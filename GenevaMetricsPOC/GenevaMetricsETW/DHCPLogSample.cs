// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace LogAnalyticsOdsApiHarness
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using Event.Ingest.Larp;
    using global::LogAnalyticsOdsApiHarness.CustomTypes;
    using Newtonsoft.Json;

    public class DhcpLogSample
    {
        private static SentinelApiConfig SentinelApiConfig { get; set; }

        static DhcpLogSample()
        {
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);
        }

        public static void SendDataToODS_DhcpLog()
        {
            // string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary
            string requestId = Guid.NewGuid().ToString("D");
            string jsonContent = File.ReadAllText("DhcpLogItems.json");

            var items = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(jsonContent);

            string dateTime = DateTime.Now.ToString("O");

            var config = new LarpUploaderConfig()
            {
                BatchSize = 100,
                MaxItemLingerTime = TimeSpan.FromMilliseconds(5000),
                WorkspaceId = SentinelApiConfig.WorkspaceId,
                JsonHeaderDataType = SentinelApiConfig.DataType,
                JsonHeaderIPName = SentinelApiConfig.IpName,
                MaxIngestorCount = 10,
                LogOptions = Event.Ingest.UploaderLogOptions.Console
            };

            var larpUploader = LarpUploadHelper.CreateLarpUploader(config);

            try
            {
                foreach (var v in items)
                {
                    larpUploader.OnNext(v);
                }

                larpUploader.OnCompleted();
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
    }
}