// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Diagnostics.Eventing.Reader;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using Newtonsoft.Json;
    using SIEMfx.SentinelWorkspaceApi;
    using SIEMfx.SentinelWorkspacePoc.CustomTypes;
    using SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers;
    using WinLog.Helpers;
    using WinLog.LogHelpers;
    using Formatting = Newtonsoft.Json.Formatting;

    public class SentinelWorkspaceLogHub
    {
        private static SentinelApiConfig SentinelApiConfig { get; set; }

        private static Exception _lastReadException;

        private static string ResourceId { get; set; } = Environment.MachineName;

        private static readonly string XmlMediaType = "application/xml";
        private static readonly string JsonMediaType = "application/json";

        private static readonly EventLogProcessor eventLogProcessor;

        private static readonly SyslogToSentinelProcessor syslogToSentinelProcessor;

        private static readonly SyslogToAzureBlob syslogToAzureBlob;

        private static int eventRecordCounter;

        public static List<EventRecord> EventRecords = new List<EventRecord>();

        public static X509Certificate2 AuthX509Certificate2 = null;

        private static bool readEventLogFileFromBeginning = true;

        private static string sentinalAuthWorkspaceKey = null;

        private static IVault KeyVault { get; set; }


        static SentinelWorkspaceLogHub()
        {
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(SentinelWorkspacePoc.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);

            // Turn on the KeyVault for use
            KeyVault = new KeyVault(SentinelApiConfig);

            // Create the processor
            syslogToSentinelProcessor = new SyslogToSentinelProcessor(SentinelApiConfig);
            
            // Create the storage container connection
            syslogToAzureBlob = new SyslogToAzureBlob(SentinelApiConfig, GetKeyVaultSecret(SentinelApiConfig.SyslogToAzureBlobStorageSecret));

            eventLogProcessor = new EventLogProcessor("Security", NewEventRecord, readEventLogFileFromBeginning);

            using (var certificateManagement = new CertificateManagement())
            {
                AuthX509Certificate2 = certificateManagement.FindCertificateByThumbprint("MY", SentinelApiConfig.CertificateThumbprint, StoreLocation.LocalMachine);
            }

            // Get the certificate from KeyVault
            string sentinalAuthCertEncoded = GetKeyVaultSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wsid");
            byte[] certFromKeyVault = Encoding.Unicode.GetBytes(sentinalAuthCertEncoded);
            // AuthX509Certificate2 = new X509Certificate2(certFromKeyVault, "SecurePassword", X509KeyStorageFlags.Exportable);

            // Get the current WorkspaceKey from KeyVault
            sentinalAuthWorkspaceKey = GetKeyVaultSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wskey");
        }
        
        private static string GetKeyVaultSecret(string secretName)
        {
            try
            {
                return KeyVault.GetSecret(secretName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public static void GetNextBatchOfRecords()
        {
            syslogToSentinelProcessor.GetNextBatchOfRecords();
        }

        public static string GetLogAnalyticsResourceId(string workspaceId)
        {
            // This interpolated string, although it looks hacked together, is correct and validation on the ODS Web service, IF it is provided as a request header,
            // requires the below structure with the required fields (subscriptions, resourceGroups, and providers, along with the correct amount of forward slashes (trial and error)
            var fqdn = GetFullyQualifiedDomainName();
            string nonAzureResourceId = $"/subscriptions/{workspaceId}/resourceGroups/none/providers/computer/physical/{fqdn}";
            return nonAzureResourceId;
        }

        private static string GetFullyQualifiedDomainName()
        {
            string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = System.Net.Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName))
            {
                hostName += domainName;
            }

            return hostName;
        }

        public static void WindowsEventsXmlFile()
        {
            string filePath = @"C:\Users\rbiles\Documents\Output\FridayDataItems.xml";
            string allFileContents = File.ReadAllText(filePath);

            UploadBatchWithSelfSigned(allFileContents, AuthX509Certificate2);
        }

        public static void LoadSecurityEventLog()
        {
            eventLogProcessor.InitializeEventLogWatcher();
        }

        public static async Task SyslogToCustomLog()
        {
            string jsonString = JsonConvert.SerializeObject(syslogToSentinelProcessor.CustomLogDictionary);
            await LogAnalyticsPublicApi.SendEventsToLogAnalytics(jsonString, SentinelApiConfig, sentinalAuthWorkspaceKey);

            if (SentinelApiConfig.StoreDataToBlobStorage)
            {
                await syslogToAzureBlob.UploadFileToBlobStorageAsync(jsonString, "CustomLog");
            }

            SentinelWorkspacePoc.PrintCustomMessage($"Uploading [{syslogToSentinelProcessor.CustomLogDictionary.Count}] Syslog Custom Logs messages to Sentinel.", ConsoleColor.Cyan);
        }
        
        public static async Task SyslogToLinuxSyslogJson()
        {
            Dictionary<string, object> jsonLinuxSyslogDictionary = new Dictionary<string, object>();
            jsonLinuxSyslogDictionary.Add("DataType", "LINUX_SYSLOGS_BLOB");
            jsonLinuxSyslogDictionary.Add("IPName", "logmanagement");
            jsonLinuxSyslogDictionary.Add("DataItems", syslogToSentinelProcessor.SyslogDictionary);
            string jsonFinalString = JsonConvert.SerializeObject(jsonLinuxSyslogDictionary);

            UploadBatchWithSelfSignedJson(jsonFinalString, AuthX509Certificate2);

            if (SentinelApiConfig.StoreDataToBlobStorage)
            {
                await syslogToAzureBlob.UploadFileToBlobStorageAsync(jsonFinalString, "LinuxSyslog");
            }

            SentinelWorkspacePoc.PrintCustomMessage($"Uploading [{syslogToSentinelProcessor.SyslogDictionary.Count}] LogManagement.Syslog messages to Sentinel.", ConsoleColor.Magenta);
        }

        public static async Task SyslogToCefSyslogJson()
        {
            Dictionary<string, object> jsonLinuxSyslogDictionary = new Dictionary<string, object>();
            jsonLinuxSyslogDictionary.Add("DataType", "SECURITY_CEF_BLOB");
            jsonLinuxSyslogDictionary.Add("IPName", "Security");
            jsonLinuxSyslogDictionary.Add("DataItems", syslogToSentinelProcessor.CefDictionary);
            string jsonFinalString = JsonConvert.SerializeObject(jsonLinuxSyslogDictionary, Formatting.Indented);

            File.WriteAllText(@"c:\temp\CefJson.json", jsonFinalString);

            UploadBatchWithSelfSignedJson(jsonFinalString, AuthX509Certificate2);

            if (SentinelApiConfig.StoreDataToBlobStorage)
            {
                await syslogToAzureBlob.UploadFileToBlobStorageAsync(jsonFinalString, "CefSyslog");
            }

            SentinelWorkspacePoc.PrintCustomMessage($"Uploading [{syslogToSentinelProcessor.CefDictionary.Count}] LogManagement.CommonSecurityLog messages to Sentinel.", ConsoleColor.Blue);
        }

        public static async Task CefFilesToSentinelProcessor()
        {
            //X509Certificate2 cert;
            GlobalLog.WriteToStringBuilderLog("Attempting to load CEF Files ", 14001);

            // Update to LINQ query to prevent attempting to load ALL files during an iteration.
            var directoryInfo = new DirectoryInfo(SentinelApiConfig.EnabledSentinelUploads.CefFileFolderToUpload);
            var orderedFileList =
                directoryInfo.EnumerateFiles("CefToSentinel*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(d => d.LastAccessTime)
                    .Select(d => d.FullName)
                    .Take(25)
                    .ToList();

            if (orderedFileList.Count > 0)
            {
                foreach (string file in orderedFileList)
                {
                    string jsonFinalString = File.ReadAllText(file);
                    UploadBatchWithSelfSignedJson(jsonFinalString, AuthX509Certificate2);
                    SentinelWorkspacePoc.PrintCustomMessage($"Uploading [{file}] LogManagement.CommonSecurityLog messages to Sentinel.", ConsoleColor.Green);

                    File.Delete(file);
                }
            }
        }

        public static void WindowsEventsFolderContents()
        {
            DirectoryInfo d = new DirectoryInfo($@"D:\OSSCWec\LAFiles");
            XmlCreationMechanism createMechanism = XmlCreationMechanism.XmlWriter;

            FileInfo[] Files = d.GetFiles("Archive*.evtx"); //Getting Text files

            Console.WriteLine($"Attempting to upload {Files.Length}");

            foreach (FileInfo file in Files)
            {
                Console.WriteLine($"FileName: {file.FullName}");
                Console.WriteLine($"\tUploading file with : {createMechanism.ToString()}", 10003);
                UploadEntireFileInBatches(file.FullName, createMechanism);

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

        private static async void UploadBatchWithSelfSigned(string payload, X509Certificate2 cert)
        {
            //var runtimeConfig = _config.RuntimeConfig;
            //var client = runtimeConfig.HttpClient;
            //if (client == null) // might happen in shutdown when client had been deallocated
            //{
            //    return null; // parallel ingestor would understand
            //}

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var clientHandler = new WebRequestHandler();
            clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

            clientHandler.ClientCertificates.Add(cert);
            var client = new HttpClient(clientHandler);

            string requestId = Guid.NewGuid().ToString("D");
            string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/EventDataService.svc/PostDataItems?api-version=2016-04-01";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-Request-ID", requestId);
            request.Headers.Add("x-ms-AzureResourceId", GetLogAnalyticsResourceId(SentinelApiConfig.WorkspaceId));
            request.Content = new StringContent(payload, Encoding.UTF8, XmlMediaType);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(XmlMediaType));
            var response = await client.SendAsync(request);
            var respText = await response.Content?.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"ODS ingest error, HttpStatus: {response.StatusCode}, Message: {respText}");
            }

            HttpContent responseContent = response.Content;
            string result = responseContent.ReadAsStringAsync().Result;
            Console.WriteLine("Return Result: " + (response.IsSuccessStatusCode ? "Success" : "Failed"));
            //Console.WriteLine("requestId: " + requestId);
            //Console.WriteLine(response);
        }

        private static async void UploadBatchWithSelfSignedJson(string payload, X509Certificate2 cert)
        {
            //var runtimeConfig = _config.RuntimeConfig;
            //var client = runtimeConfig.HttpClient;
            //if (client == null) // might happen in shutdown when client had been deallocated
            //{
            //    return null; // parallel ingestor would understand
            //}

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var clientHandler = new WebRequestHandler();
            clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

            clientHandler.ClientCertificates.Add(cert);
            var client = new HttpClient(clientHandler);

            string requestId = Guid.NewGuid().ToString("D");
            string url = $"https://{SentinelApiConfig.WorkspaceId}.{SentinelApiConfig.OdsEndpointUri}/OperationalData.svc/PostJsonDataItems?api-version=2016-04-01";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Accept", JsonMediaType);
            request.Headers.Add("X-Request-ID", requestId);
            request.Headers.Add("x-ms-AzureResourceId", GetLogAnalyticsResourceId(SentinelApiConfig.WorkspaceId));
            request.Content = new StringContent(payload, Encoding.UTF8, JsonMediaType);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            var response = await client.SendAsync(request);
            var respText = await response.Content?.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"ODS ingest error, HttpStatus: {response.StatusCode}, Message: {respText}");
            }

            HttpContent responseContent = response.Content;
            string result = responseContent.ReadAsStringAsync().Result;
            // Console.WriteLine("Return Result: " + (response.IsSuccessStatusCode ? "Success" : "Failed"));
            // Console.WriteLine("requestId: " + requestId);
            // Console.WriteLine(response);
        }

        private static readonly object recordLock = new object();

        private static void NewEventRecord(EventRecord eventRecord)
        {
            try
            {
                eventRecordCounter++;

                // Add the event
                EventRecords.Add(eventRecord);

                if (EventRecords.Count == 100)
                {
                    Console.WriteLine($"Windows Event Counter: {eventRecordCounter}");

                    lock (recordLock)
                    {
                        List<EventRecord> newList = EventRecords.GetRange(0, EventRecords.Count);

                        EventRecords = new List<EventRecord>();

                        string currentRecords = SerializeItems(newList);
                        UploadBatchWithSelfSigned(currentRecords, AuthX509Certificate2);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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

        private static void UploadEntireFileInBatches(string fileFullName, XmlCreationMechanism creationMechanism, int batchCount = 200)
        {
            WindowsEventPayload payload = GetNewPayloadObject();
            bool useEventIngest = false;

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
                    //Console.WriteLine($"\tRecordCount: {payload.Uploader.ItemCount:N0}");
                    //Console.WriteLine(
                    //    $"\tEPS for Conversion: {payload.Uploader.ItemCount / fileStopwatch.Elapsed.TotalSeconds:N3}");

                    //// Wait for upload to complete, and report
                    //payload.Uploader.OnCompleted();
                    //uploaderStopwatch.Stop();

                    //Console.WriteLine($"Upload Completed...");
                    //Console.WriteLine($"\tEPS for Upload with Event.Ingest to MMA-API: {payload.Uploader.ItemCount / uploaderStopwatch.Elapsed.TotalSeconds:N3}");
                    //Console.WriteLine($"\t Average for batch with Event.Ingest to MMA-API: {payload.BatchItemCount / payload.BatchTimeSpan.TotalSeconds:N3}");
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
                    singleBatch => { UploadBatchToLogAnalytics(payload.GetUploadBatch(singleBatch), AuthX509Certificate2); });

                fileStopwatch.Stop();
                Console.WriteLine($"\tEPS for Upload to MMA-API: {payload.DataItems.Count / fileStopwatch.Elapsed.TotalSeconds:N3}");
            }
            catch (Exception e)
            {
                GlobalLog.WriteToStringBuilderLog(e.ToString(), 14008);
            }
        }

        public static string SerializeItems(IList<EventRecord> items)
        {
            var sizeEst = 6200 * items.Count + 2000; //6K per item plus 2k for header, footer
            var sb = new StringBuilder(sizeEst);
            var stt = new XmlWriterSettings();
            stt.ConformanceLevel = ConformanceLevel.Fragment;
            using (var writer = XmlWriter.Create(sb, stt))
            {
                // Write DataItems root element
                // "<DataItems IPName=\"Security\" ManagementGroupId=\"00000000-0000-0000-0000-000000000001\" HealthServiceSourceId=\"{0}\" DataType=\"SECURITY_WEF_EVENT_BLOB\">";
                writer.WriteStartElement("DataItems");
                writer.WriteAttributeString("IPName", "Security");
                writer.WriteAttributeString("ManagementGroupId", "00000000-0000-0000-0000-000000000001");
                writer.WriteAttributeString("HealthServiceSourceId", SentinelApiConfig.WorkspaceId);
                writer.WriteAttributeString("DataType", "SECURITY_WEF_EVENT_BLOB");

                foreach (var eventRecord in items)
                {
                    try
                    {
                        WriteEventRecordXml(writer, eventRecord);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in SerializeItems: {ex}");
                    }
                }

                writer.WriteEndElement(); //  </DataItems>    
                writer.Flush();
                var xml = sb.ToString();
                return xml;
            }
        }

        // It turns out individual events might be malformed and throw exc on attempt to read a property, so we are covering it with try/catch
        private static void WriteEventRecordXml(XmlWriter writer, EventRecord eventRecord)
        {
            _lastReadException = null;
            // DataItem header
            writer.WriteStartElement("DataItem");
            try
            {
                writer.WriteAttributeString("type", "System.Event.LinkedData");
                // 5/13/2020 - 'time-created controversy' Workaround/fix for DSRE team; we force current UTC here
                var eventTimeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
                //var eventTimeUtc = eventRecord.TimeCreated?.ToUniversalTime()
                //                       .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ") ?? string.Empty;
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", SentinelApiConfig.WorkspaceId);

                //Nested elements
                writer.WriteElementString("EventOriginId", "{7C384BE3-8EBD-4B86-A392-357AA34750C5}");
                var pubId = SafeRead(() => (eventRecord.ProviderId ?? Guid.Empty).ToString());
                writer.WriteElementString("PublisherId", pubId);
                writer.WriteElementString("PublisherName", SafeRead(() => eventRecord.ProviderName));
                writer.WriteElementString("EventSourceName", SafeRead(() => eventRecord.ProviderName));
                writer.WriteElementString("Channel", SafeRead(() => eventRecord.LogName) ?? "Unknown");
                writer.WriteElementString("LoggingComputer", SafeRead(() => eventRecord.MachineName));
                writer.WriteElementString("EventNumber", SafeRead(() => eventRecord.Id.ToString()));
                writer.WriteElementString("EventCategory", SafeRead(() => (eventRecord.Task ?? 0).ToString()));
                writer.WriteElementString("EventLevel", SafeRead(() => (eventRecord.Level ?? 0).ToString()));
                writer.WriteElementString("UserName", "N/A");
                writer.WriteElementString("RawDescription", string.Empty);
                writer.WriteElementString("LCID", "1033");
                writer.WriteElementString("CollectDescription", "True");

                // EventData with nested data item
                writer.WriteStartElement("EventData");
                writer.WriteStartElement("DataItem");
                writer.WriteAttributeString("type", "System.XmlData");
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", SentinelApiConfig.WorkspaceId);
                SafeWriteExtendedData(writer, eventRecord);
                writer.WriteFullEndElement(); // close DataItem
                writer.WriteFullEndElement(); // close EventData
                // end EventData

                writer.WriteElementString("EventDisplayNumber", SafeRead(() => eventRecord.Id.ToString()));
                writer.WriteElementString("ManagedEntityId", "{D056ADDA-9675-7690-CC92-41AA6B90CC05}");
                writer.WriteElementString("RuleId", "{1F68E37D-EC73-9BD3-92D5-C236C995FA0A}");
            }
            finally
            {
                if (_lastReadException != null)
                {
                    // if exception happened - write it into 'EventDescription' element.
                    writer.WriteElementString("EventDescription", _lastReadException.ToString() ?? string.Empty);
                    Console.WriteLine($"WriteEventRecordXML error: {_lastReadException}");
                }
                else
                {
                    writer.WriteElementString("EventDescription", string.Empty);
                }

                writer.WriteFullEndElement(); // </DataItem>
            }
        }

        private static TR SafeRead<TR>(Func<TR> reader)
        {
            try
            {
                return reader();
            }
            catch (Exception ex)
            {
                _lastReadException = ex;
                return default(TR);
            }
        }

        // 7/10/2020 - Russell's new version
        private static void SafeWriteExtendedData(XmlWriter writer, EventRecord record)
        {
            try
            {
                var xml = record.ToXml();
                var cleanXml = XmlVerification.VerifyAndRepairXml(xml);
                var xAllData = XElement.Parse(cleanXml);
                var xEventData = xAllData.Element(ElementNames.EventData);
                var xUserData = xAllData.Element(ElementNames.UserData);

                if (xEventData == null && xUserData == null)
                {
                    return;
                }

                // If EventData has value, add as a Data attribute the TimeCreated and EventRecordId
                if (xEventData != null)
                {
                    XName xDataName = XName.Get("Data", xEventData.Name.Namespace.NamespaceName);
                    // 5/13/2020 - 'time-created controversy' Workaround/fix for DSRE team; we put original record.TimeCreated here
                    xEventData.Add(new XElement(xDataName, new XAttribute("Name", "TimeCreated"), record.TimeCreated));

                    // Add event record Id and RecordId data; EventRecordId is in SysData
                    xEventData.Add(new XElement(xDataName, new XAttribute("Name", "EventRecordId"), record.RecordId));

                    writer.WriteRaw(xEventData.ToString());
                }

                // If UserData has value, add as a XElement  the TimeCreated and EventRecordId
                if (xUserData != null)
                {
                    var firstLevelElements = xUserData.Elements();
                    var secondLevelElements = firstLevelElements.Elements();

                    // If there are no secondary elements, add to the root elements
                    var levelElements = secondLevelElements as XElement[] ?? secondLevelElements.ToArray();
                    if (!levelElements.Any())
                    {
                        xUserData.Add(new XElement("TimeCreated", record.TimeCreated));
                        xUserData.Add(new XElement("EventRecordId", record.RecordId));

                        writer.WriteRaw(xUserData.ToString());
                        return;
                    }

                    // If there are secondary elements, add to these root elements
                    if (levelElements.Any())
                    {
                        foreach (var data in xUserData.Elements())
                        {
                            // Simply add above the first element, and then break.
                            foreach (XElement element in data.Elements())
                            {
                                element.AddBeforeSelf(new XElement("TimeCreated", record.TimeCreated));
                                element.AddBeforeSelf(new XElement("EventRecordId", record.RecordId));

                                break;
                            }
                        }

                        writer.WriteRaw(xUserData.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                SafeWriteError(writer, ex);
            }
        }

        private static void SafeWriteError(XmlWriter writer, Exception ex)
        {
            writer.WriteStartElement("EventData", "http://schemas.microsoft.com/win/2004/08/events/event");
            // Exc.Message
            writer.WriteStartElement("Data");
            writer.WriteAttributeString("Name", "Error");
            writer.WriteString(ex.Message);
            writer.WriteFullEndElement();
            // Exc.ToString(), with stack trace
            writer.WriteStartElement("Data");
            writer.WriteAttributeString("Name", "ErrorDetails");
            writer.WriteString(ex.StackTrace);
            writer.WriteFullEndElement();
            // Explanation
            writer.WriteStartElement("Data");
            writer.WriteAttributeString("Name", "ErrorComment");
            writer.WriteString("Failed to retrieve Extended data, EventRecord.ToXml() failed.");
            writer.WriteFullEndElement();
            //close EventData
            writer.WriteFullEndElement(); // </EventData>
        }
    }
}