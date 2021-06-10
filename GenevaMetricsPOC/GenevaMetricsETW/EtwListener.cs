// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Reactive.Kql;
using System.Reactive.Kql.CustomTypes;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using GenevaETW.API;
using GenevaETW.API.CustomTypes;
using GenevaEtwPOC.CustomTypes;
using Newtonsoft.Json;
using Tx.Windows;

namespace GenevaEtwPOC
{
    public class EtwListener : IDisposable
    {
        private static readonly string XmlMediaType = "application/xml";
        private static readonly string JsonMediaType = "application/json";

        private readonly object uploadLock = new object();


        public EtwListener(SentinelApiConfig sentinelApiConfig, EtwListenerConfig etwListenerConfig,
            bool useEventIngest)
        {
            EtwListenerConfig = etwListenerConfig;
            SentinelApiConfig = sentinelApiConfig;
            UseEventIngest = useEventIngest;

            // Initialize on the first heartbeat after the HostBuilder loads all configs
            if (syntheticCounterManager == null && SentinelApiConfig.SloMetricsConfiguration != null)
            {
                // Set up the SLO metrics logging mechanism
                var sloMetricsConfiguration = new GenevaMdmConfiguration
                {
                    MetricsNamespace = SentinelApiConfig.SloMetricsConfiguration.MetricsNamespace,
                    MetricsAccount = SentinelApiConfig.SloMetricsConfiguration.MetricsAccount,
                    LocationId = SentinelApiConfig.SloMetricsConfiguration.LocationId,
                    MinimumValue = SentinelApiConfig.SloMetricsConfiguration.MinimumValue,
                    BucketSize = SentinelApiConfig.SloMetricsConfiguration.BucketSize,
                    BucketCount = SentinelApiConfig.SloMetricsConfiguration.BucketCount
                };

                syntheticCounterManager = new SyntheticCounterManager(sloMetricsConfiguration);
            }

            // Turn on the Provider, and listen
            InitializeEtwListener();
        }

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

        private X509Certificate2 logAnalyticsX509Certificate2 { get; set; }

        private SyntheticCounterManager syntheticCounterManager { get; }

        public void Dispose()
        {
            // Stop the providers on class disposal
            EtwProviderSession(EtwListenerConfig.SessionName, EtwListenerConfig.ProviderId, false);
        }

        public void InitializeEtwListener()
        {
            payload = GetNewPayloadObject();

            var configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            EtwProviderSession(EtwListenerConfig.SessionName, EtwListenerConfig.ProviderId, true);
            var _etw = EtwTdhObservable.FromSession(EtwListenerConfig.SessionName);

            KqlNodeHub = KqlNodeHub.FromKqlQuery(_etw, DefaultOutput, EtwListenerConfig.ObservableName,
                EtwListenerConfig.KqlQuery);

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            var textOfJsonConfig =
                File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);

            if (SentinelApiConfig.UseMmaCertificate)
                logAnalyticsX509Certificate2 =
                    CertificateManagement.FindOdsCertificateByWorkspaceId(SentinelApiConfig.WorkspaceId);
            else
                logAnalyticsX509Certificate2 = CertificateManagement.FindCertificateByThumbprint("MY",
                    SentinelApiConfig.CertificateThumbprint, StoreLocation.LocalMachine);

            GlobalLog.WriteToStringBuilderLog($"SampleData load [{configurationFile}].", 14001);
            var sampleData =
                File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"XMLFile1.xml"));
            UploadBatchToLogAnalytics(sampleData, logAnalyticsX509Certificate2);
        }

        private void
            DefaultOutput(
                KqlOutput e) // The type should not be called Detection nor Alert and should not be in separate namespace
        {
            var output = e.Output; // this is no longer alert. "Output" is better name for the property

            if (DateTime.UtcNow.AddSeconds(-30) > lastUploadTime)
                lock (uploadLock)
                {
                    // Uplaod the current cache of records
                    // UploadPayloadCacheInBatches(logAnalyticsX509Certificate2);

                    // Update last upload time and create a new payload object
                    lastUploadTime = DateTime.UtcNow;
                    payload = GetNewPayloadObject();
                    Console.WriteLine(string.Empty);
                }

            payload.AddEvent(this, output, UseEventIngest);

            var eventJson = JsonConvert.SerializeObject(output);

            StringBuilder sbRecord = new StringBuilder("Event:");
            foreach (var outputKey in output.Keys)
            {
                sbRecord.Append($" {outputKey}: {output[outputKey]}");
            }

            Console.WriteLine(sbRecord.ToString());

            // Create SLO record for latency of files transferred
            syntheticCounterManager.InsertEtwEventTcpNetwork($"{Environment.MachineName}:GenevaEtwPOC", output);
        }

        private void EtwProviderSession(string sessionName, Guid providerId, bool startSession)
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                throw new Exception("To use ETW real-time session you must be administrator");

            var logman = Process.Start("logman.exe", "stop " + sessionName + " -ets");
            logman.WaitForExit();

            if (!startSession) return;

            logman = Process.Start("logman.exe",
                "create trace " + sessionName + " -rt -nb 2 2 -bs 1024 -p {" + providerId +
                "} 0xffffffffffffffff -ets");
            logman.WaitForExit();
        }

        private WindowsEventPayload GetNewPayloadObject()
        {
            return new WindowsEventPayload
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
            var fileStopwatch = new Stopwatch();
            var uploaderStopwatch = Stopwatch.StartNew();

            try
            {
                fileStopwatch.Start();

                // Split into upload chunks
                var splitLIsts = payload.SplitListIntoChunks<string>(batchCount);
                fileStopwatch.Restart();

                Parallel.ForEach(splitLIsts, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 8
                    },
                    singleBatch => { UploadBatchToLogAnalytics(payload.GetUploadBatch(singleBatch), cert); });

                fileStopwatch.Stop();
                Console.WriteLine(
                    $"\tEPS for Upload to MMA-API: {payload.DataItems.Count / fileStopwatch.Elapsed.TotalSeconds:N3}");
            }
            catch (Exception e)
            {
                GlobalLog.WriteToStringBuilderLog(e.ToString(), 14008);
            }
        }

        private async void UploadBatchToLogAnalytics(string payload, X509Certificate2 cert)
        {
            try
            {
                //string requestId = Guid.NewGuid().ToString("D");

                //WebRequestHandler clientHandler = new WebRequestHandler();
                //clientHandler.ClientCertificates.Add(cert);
                //var client = new HttpClient(clientHandler);

                //string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/EventDataService.svc/PostDataItems?api-version=2016-04-01";
                //client.DefaultRequestHeaders.Add("X-Request-ID", requestId);

                //HttpContent httpContent = new StringContent(payload, Encoding.UTF8);
                //httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                //HttpContent responseContent = response.Result.Content;
                //string result = responseContent.ReadAsStringAsync().Result;
                //// Console.WriteLine("Return Result: " + result);
                //Console.WriteLine("requestId: " + requestId);
                //// Console.WriteLine(response.Result);

                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var mtlsHandler = new HttpClientHandler
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };

                mtlsHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(mtlsHandler);

                string requestId = Guid.NewGuid().ToString("D");

                string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/EventDataService.svc/PostDataItems?api-version=2016-04-01";
                client.DefaultRequestHeaders.Add("X-Request-ID", requestId);
                client.DefaultRequestHeaders.Add("x-ms-AzureResourceId", GetLogAnalyticsResourceId(SentinelApiConfig.WorkspaceId));

                HttpContent httpContent = new StringContent(payload, Encoding.UTF8, "application/xml");
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
                Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine("Return Result: " + result);
                Console.WriteLine("requestId: " + requestId);
                Console.WriteLine(response.Result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }

        public static string GetLogAnalyticsResourceId(string workspaceId)
        {
            // This interpolated string, although it looks hacked together, is correct and validation on the ODS Web service, IF it is provided as a request header,
            // requires the below structure with the required fields (subscriptions, resourceGroups, and providers, along with the correct amount of forward slashes (trial and error)
            var fqdn = GetFullyQualifiedDomainName();
            var nonAzureResourceId =
                $"/subscriptions/{workspaceId}/resourceGroups/none/providers/computer/physical/{fqdn}";
            return nonAzureResourceId;
        }

        private static string GetFullyQualifiedDomainName()
        {
            var domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            var hostName = Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName)) hostName += domainName;

            return hostName;
        }
    }
}