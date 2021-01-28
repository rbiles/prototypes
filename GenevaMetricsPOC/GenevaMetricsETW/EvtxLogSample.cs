// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace LogAnalyticsOdsApiHarness
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using global::LogAnalyticsOdsApiHarness.CustomTypes;
    using Newtonsoft.Json;
    using WinLog.LogHelpers;

    public class EvtxLogSample
    {
        private static SentinelApiConfig SentinelApiConfig { get; set; }

        private static string ResourceId { get; set; } = Environment.MachineName;


        static EvtxLogSample()
        {
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);
        }

        public static void UploadFolderContents()
        {
            DirectoryInfo d = new DirectoryInfo($@"D:\OSSCWec\TestEventLogs");
            XmlCreationMechanism createMechanism = XmlCreationMechanism.XmlWriter;

            FileInfo[] Files = d.GetFiles("Archive*.evtx"); //Getting Text files
            
            X509Certificate2 cert = null;
            if (SentinelApiConfig.UseMmaCertificate)
            {
                cert = CertificateManagement.FindOdsCertificateByWorkspaceId(SentinelApiConfig.WorkspaceId);
            }
            else
            {
                cert = CertificateManagement.FindCertificateByThumbprint("MY", SentinelApiConfig.CertificateThumbprint, StoreLocation.LocalMachine);
            }

            Console.WriteLine($"Attempting to upload {Files.Length}");

            foreach (FileInfo file in Files)
            {
                Console.WriteLine($"FileName: {file.FullName}");
                Console.WriteLine($"\tUploading file with : {createMechanism.ToString()}", 10003);
                UploadEntireFileInBatches(file.FullName, cert, createMechanism);

                if (File.Exists(file.FullName))
                {
                    Console.WriteLine($"\tDeleting File: {file.FullName}");
                    File.Delete(file.FullName);
                }
            }
        }

        private static void UploadBatchToLogAnalytics(string payload, X509Certificate2 cert)
        {
            try
            {
                string requestId = Guid.NewGuid().ToString("D");

                WebRequestHandler clientHandler = new WebRequestHandler();
                clientHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(clientHandler);

                string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/EventDataService.svc/PostDataItems?api-version=2016-04-01";
                client.DefaultRequestHeaders.Add("X-Request-ID", requestId);
                client.DefaultRequestHeaders.Add("x-ms-AzureResourceId", ResourceId);

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

        private static WindowsEventPayload GetNewPayloadObject()
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

        private static void UploadEntireFileInBatches(string fileFullName, X509Certificate2 cert, XmlCreationMechanism creationMechanism, int batchCount = 200)
        {
            WindowsEventPayload payload = GetNewPayloadObject();
            bool useEventIngest = false;
            if (useEventIngest)
            {
                payload.InitializeEventIngest();
            }

            // Set the ResourceId for upload
            ResourceId = payload.GetLogAnalyticsResourceId(SentinelApiConfig.WorkspaceId);

            Stopwatch fileStopwatch = new Stopwatch();
            Stopwatch uploaderStopwatch = Stopwatch.StartNew();

            try
            {
                fileStopwatch.Start();
                var log = EvtxEnumerable.ReadEvtxFile(fileFullName);

                Parallel.ForEach(log, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 8,
                    },
                    eventRecord => { payload.AddEvent(eventRecord, useEventIngest, creationMechanism); });

                fileStopwatch.Stop();

                if (useEventIngest)
                {
                    Console.WriteLine($"\tRecordCount: {payload.Uploader.ItemCount:N0}");
                    Console.WriteLine(
                        $"\tEPS for Conversion: {payload.Uploader.ItemCount / fileStopwatch.Elapsed.TotalSeconds:N3}");

                    // Wait for upload to complete, and report
                    payload.Uploader.OnCompleted();
                    uploaderStopwatch.Stop();

                    Console.WriteLine($"Upload Completed...");
                    Console.WriteLine($"\tEPS for Upload with Event.Ingest to MMA-API: {payload.Uploader.ItemCount / uploaderStopwatch.Elapsed.TotalSeconds:N3}");
                    Console.WriteLine($"\t Average for batch with Event.Ingest to MMA-API: {payload.BatchItemCount / payload.BatchTimeSpan.TotalSeconds:N3}");
                }
                else
                {
                    Console.WriteLine($"\tRecordCount: {payload.DataItems.Count:N0}");
                    string output =
                        $"\tEPS for Conversion: {payload.DataItems.Count / fileStopwatch.Elapsed.TotalSeconds:N3}";
                    Console.WriteLine(output);
                }

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
    }
}