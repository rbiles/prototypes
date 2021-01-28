// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Kql;
using System.Reactive.Kql.CustomTypes;
using System.Security.Principal;
using LogAnalyticsOdsApiHarness.CustomTypes;
using Newtonsoft.Json;
using Tx.Windows;

namespace LogAnalyticsOdsApiHarness
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    public class EtwListener : IDisposable
    {
        public SentinelApiConfig SentinelApiConfig { get; set; }

        // DNS Provider
        public string SessionName { get; set; }

        public Guid ProviderIdGuid { get; set; }

        public EtwListenerType EtwListenerType { get; set; }

        public KqlNodeHub KqlNodeHub { get; set; }

        public bool UseEventIngest { get; set; }

        public WindowsEventPayload payload { get; set; }

        public EtwListenerConfig EtwListenerConfig { get; set; }

        private DateTime lastUploadTime { get; set; } = DateTime.UtcNow;

        private readonly object uploadLock = new object();

        private X509Certificate2 logAnalyticsX509Certificate2 { get; set; }

        public EtwListener(SentinelApiConfig sentinelApiConfig, EtwListenerConfig etwListenerConfig, bool useEventIngest)
        {
            EtwListenerConfig = etwListenerConfig;
            SentinelApiConfig = sentinelApiConfig;
            UseEventIngest = useEventIngest;

            // Turn on the Provider, and listen
            InitializeEtwListener();
        }

        public void InitializeEtwListener()
        {
            payload = GetNewPayloadObject();

            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            EtwProviderSession(EtwListenerConfig.SessionName, EtwListenerConfig.ProviderId, true);
            IObservable<IDictionary<string, object>> _etw = EtwTdhObservable.FromSession(EtwListenerConfig.SessionName);

            KqlNodeHub = KqlNodeHub.FromKqlQuery(_etw, DefaultOutput, EtwListenerConfig.ObservableName, EtwListenerConfig.KqlQuery);

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);

            if (SentinelApiConfig.UseMmaCertificate)
            {
                logAnalyticsX509Certificate2 = CertificateManagement.FindOdsCertificateByWorkspaceId(SentinelApiConfig.WorkspaceId);
            }
            else
            {
                logAnalyticsX509Certificate2 = CertificateManagement.FindCertificateByThumbprint("MY", SentinelApiConfig.CertificateThumbprint, StoreLocation.LocalMachine);
            }
        }

        private void DefaultOutput(KqlOutput e) // The type should not be called Detection nor Alert and should not be in separate namespace
        {
            var output = e.Output; // this is no longer alert. "Output" is better name for the property

            if (DateTime.UtcNow.AddSeconds(-30) > lastUploadTime)
            {
                lock (uploadLock)
                {
                    // Uplaod the current cache of records
                    UploadPayloadCacheInBatches(logAnalyticsX509Certificate2);

                    // Update last upload time and create a new payload object
                    lastUploadTime = DateTime.UtcNow;
                    payload = GetNewPayloadObject();
                }
            }

            payload.AddEvent(this, output, UseEventIngest);

            Console.WriteLine(
                $"EtwListenerConfig.ObservableName: [{EtwListenerConfig.ObservableName}] TimeCreated: {output["TimeCreated"]}  EventId: {output["EventId"]}  ProcessName: [{output["ProcessName"]}]");
        }

        private void EtwProviderSession(string sessionName, Guid providerId, bool startSession)
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                throw new Exception("To use ETW real-time session you must be administrator");

            Process logman = Process.Start("logman.exe", "stop " + sessionName + " -ets");
            logman.WaitForExit();

            if (!startSession)
            {
                return;
            }

            logman = Process.Start("logman.exe", "create trace " + sessionName + " -rt -nb 2 2 -bs 1024 -p {" + providerId + "} 0xffffffffffffffff -ets");
            logman.WaitForExit();
        }

        public void Dispose()
        {
            // Stop the providers on class disposal
            EtwProviderSession(EtwListenerConfig.SessionName, EtwListenerConfig.ProviderId, false);
        }

        private WindowsEventPayload GetNewPayloadObject()
        {
            return new WindowsEventPayload()
            {
                DataType = SentinelApiConfig.DataType,
                IpName = SentinelApiConfig.IpName,
                WorkspaceId = SentinelApiConfig.WorkspaceId,
                ManagementGroupId = SentinelApiConfig.ManagementGroupId,
                SentinenApiConfig = SentinelApiConfig
            };
        }

        private void UploadPayloadCacheInBatches(X509Certificate2 cert, int batchCount = 200)
        {
            Stopwatch fileStopwatch = new Stopwatch();
            Stopwatch uploaderStopwatch = Stopwatch.StartNew();

            try
            {
                fileStopwatch.Start();

                // Split into upload chunks
                var splitLIsts = payload.SplitListIntoChunks<string>(batchCount);
                fileStopwatch.Restart();

                Parallel.ForEach(splitLIsts, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 8,
                    },
                    singleBatch => { UploadBatchToLogAnalytics(payload.GetUploadBatch(singleBatch), cert); });

                fileStopwatch.Stop();
                Console.WriteLine($"\tEPS for Upload to MMA-API: {payload.DataItems.Count / fileStopwatch.Elapsed.TotalSeconds:N3}");
            }
            catch (Exception e)
            {
                GlobalLog.WriteToStringBuilderLog(e.ToString(), 14008);
            }
        }

        private void UploadBatchToLogAnalytics(string payload, X509Certificate2 cert)
        {
            try
            {
                string requestId = Guid.NewGuid().ToString("D");

                WebRequestHandler clientHandler = new WebRequestHandler();
                clientHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(clientHandler);

                string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/EventDataService.svc/PostDataItems?api-version=2016-04-01";
                client.DefaultRequestHeaders.Add("X-Request-ID", requestId);

                HttpContent httpContent = new StringContent(payload, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                // Console.WriteLine("Return Result: " + result);
                Console.WriteLine("requestId: " + requestId);
                // Console.WriteLine(response.Result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
    }
}