// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Kusto.Data.Net.Client;
    using KustoTemplate;
    using Newtonsoft.Json;
    using SIEMfx.SentinelWorkspacePoc.CustomTypes;

    public class SyslogToSentinelProcessor
    {
        public SentinelApiConfig SentinelApiConfig { get; private set; }

        public List<Dictionary<string, object>> CefDictionary { get; set; }

        public List<Dictionary<string, object>> CustomLogDictionary { get; set; }

        public List<Dictionary<string, object>> SyslogDictionary { get; set; }

        public List<string> RawCefMessageList { get; set; }

        public SyslogToSentinelProcessor(SentinelApiConfig sentinelApiConfig)
        {
            InvalidState = false;
            SentinelApiConfig = sentinelApiConfig;

            GlobalLog.WriteToStringBuilderLog("Loading sample Syslog XML [SampleCefRecords.txt].", 14001);
            RawCefMessageList = new List<string>(File.ReadAllLines(Path.Combine(SentinelWorkspacePoc.GetExecutionPath(), $"SampleCefRecords.txt")));
        }

        public void GetNextBatchOfRecords()
        {
            // Initialize local dictionaries for this iteration.
            CefDictionary = new List<Dictionary<string, object>>();
            CustomLogDictionary = new List<Dictionary<string, object>>();
            SyslogDictionary = new List<Dictionary<string, object>>();

            Stopwatch queryTimer = Stopwatch.StartNew();

            // Query file information
            FileInfo fileInfo = new FileInfo(Path.Combine(SentinelWorkspacePoc.GetExecutionPath(), $"SyslogToSentinel.kql"));
            string textOfKustoTemplate = File.ReadAllText(fileInfo.FullName);

            // Create a single connection to be used for all queries.
            string connectionString =
                $"Data Source=https://{SentinelApiConfig.KustoDataSourceConfig.ClusterUri}:443;Initial Catalog={SentinelApiConfig.KustoDataSourceConfig.Database};AAD Federated Security=True";
            var cslQueryProvider = KustoClientFactory.CreateCslQueryProvider(connectionString);

            // Use the KustoTemplate functionality
            QueryTemplate template = new QueryTemplate(textOfKustoTemplate);
            List<Dictionary<string, object>> result = template.ExecuteForDictionary(cslQueryProvider, null);

            queryTimer.Stop();
            SentinelWorkspacePoc.PrintCustomMessage($"{fileInfo.Name} returned {result.Count} records in {queryTimer.Elapsed.TotalSeconds:N3} seconds.", ConsoleColor.Yellow);



            // Massage the Syslog Dictionary 
            foreach (Dictionary<string, object> syslogRecordDictionary in result)
            {
                Dictionary<string, object> linuxSyslogRecord = new Dictionary<string, object>();
                linuxSyslogRecord.Add("TimeStamp", syslogRecordDictionary["DeviceTimestamp"]);
                linuxSyslogRecord.Add("Host", syslogRecordDictionary["HostName"]);
                linuxSyslogRecord.Add("HostIp", syslogRecordDictionary["SourceIpAddress"]);
                linuxSyslogRecord.Add("ProcessId", syslogRecordDictionary["ProcId"]);
                linuxSyslogRecord.Add("Facility", syslogRecordDictionary["Facility"]);
                linuxSyslogRecord.Add("Severity", syslogRecordDictionary["Severity"]);
                linuxSyslogRecord.Add("Message", syslogRecordDictionary["Payload"]);
                linuxSyslogRecord.Add("AppName", syslogRecordDictionary["AppName"]);
                linuxSyslogRecord.Add("MsgId", syslogRecordDictionary["MsgId"]);

                SyslogDictionary.Add(linuxSyslogRecord);
            }

            Random random = new Random();

            SyslogToCef syslogToCef = new SyslogToCef();

            // Massage the CEF Dictionary 
            foreach (Dictionary<string, object> cefRecordDictionary in result)
            {
                Dictionary<string, object> currentRecord = syslogToCef.ConvertSyslogToCef(cefRecordDictionary);

                //CefDictionary.Add(currentRecord);

                Dictionary<string, object> cefRecord = new Dictionary<string, object>();
                cefRecord.Add("Timestamp", $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
                cefRecord.Add("EventTime", cefRecordDictionary["DeviceTimestamp"]);
                cefRecord.Add("Host", cefRecordDictionary["HostName"]);
                cefRecord.Add("HostIP", cefRecordDictionary["SourceIpAddress"]);
                cefRecord.Add("ident", "CEF");
                cefRecord.Add("Facility", cefRecordDictionary["Facility"]);
                cefRecord.Add("Severity", currentRecord["Severity"]);
                cefRecord.Add("Message", currentRecord["Message"]);

                CefDictionary.Add(cefRecord);
            }

            // Massage the CustomLog dictionary
            foreach (Dictionary<string, object> customLogRecordsDictionary in result)
            {
                customLogRecordsDictionary["ExtractedData"] = JsonConvert.SerializeObject(customLogRecordsDictionary["ExtractedData"]);
                customLogRecordsDictionary["LogFileLineage"] = JsonConvert.SerializeObject(customLogRecordsDictionary["LogFileLineage"]);

                CustomLogDictionary.Add(customLogRecordsDictionary);
            }
        }

        public bool InvalidState { get; set; }
    }
}