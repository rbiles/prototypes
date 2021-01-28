//using System;
//using System.Collections.Generic;
//using System.Diagnostics.Eventing.Reader;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Reflection;
//using System.Security.Cryptography.X509Certificates;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Xml.Linq;
//using Microsoft.Win32;
//using Newtonsoft.Json;

//namespace LogAnalyticsOdsApiHarness
//{
//    internal class LocalSecurityLog
//    {
//        private static readonly List<string> sbLog = new List<string>();

//        private static string GetExecutionPath()
//        {
//            var path = Assembly.GetExecutingAssembly().Location;
//            var directory = Path.GetDirectoryName(path);
//            return directory;
//        }

//        private static void PrintCurrentConfiguration(OdsPrivateApiConfig odsPrivateApiConfig)
//        {
//            GlobalLog.WriteToStringBuilderLog($"");
//            GlobalLog.WriteToStringBuilderLog($"Running CurrentConfiguration");

//            PropertyInfo[] properties = typeof(OdsPrivateApiConfig).GetProperties();
//            foreach (PropertyInfo property in properties)
//            {
//                var odsPrivateApiConfigFieldValue = property.GetValue(odsPrivateApiConfig, null);
//                if (odsPrivateApiConfigFieldValue == null ||
//                    string.IsNullOrEmpty(odsPrivateApiConfigFieldValue.ToString()))
//                {
//                    continue;
//                }

//                // Remove the KQL query from the ARIS call
//                if (property.Name.Equals("EndpointRetryPolicy", StringComparison.InvariantCultureIgnoreCase))
//                {
//                    PropertyInfo[] propertiesRetry = typeof(ScubaRetryPolicy).GetProperties();
//                    foreach (PropertyInfo propertyRetry in propertiesRetry)
//                    {
//                        var retryFieldValue = propertyRetry.GetValue(odsPrivateApiConfigFieldValue, null);
//                        if (retryFieldValue == null ||
//                            string.IsNullOrEmpty(retryFieldValue.ToString()))
//                        {
//                            continue;
//                        }

//                        GlobalLog.WriteToStringBuilderLog($"\t{propertyRetry.Name}: {retryFieldValue}");
//                    }
//                }
//                else
//                {
//                    GlobalLog.WriteToStringBuilderLog($"\t{property.Name}: {odsPrivateApiConfigFieldValue}");
//                }
//            }
//        }

//        public static void MonitorLocalSecurityLog()
//        {
//            try
//            {
//                string configurationFile = $"OdsPrivateApiConfig_MsrcAsi.json";

//                GlobalLog.WriteToStringBuilderLog($"Loading config {configurationFile}.", 14001);
//                string textOfJsonConfig = File.ReadAllText(Path.Combine(GetExecutionPath(), $"{configurationFile}"));
//                List<OdsPrivateApiConfig> odsPrivateApiConfigList = JsonConvert.DeserializeObject<List<OdsPrivateApiConfig>>(textOfJsonConfig);
//                OdsPrivateApiConfig odsPrivateApiConfig = odsPrivateApiConfigList.FirstOrDefault();

//                if (odsPrivateApiConfig == null)
//                {
//                    GlobalLog.WriteToStringBuilderLog($"Invalid config {configurationFile}.", 14002);
//                    return;
//                }

//                PrintCurrentConfiguration(odsPrivateApiConfig);

//                if (!odsPrivateApiConfig.MmaEndpointActive && !odsPrivateApiConfig.OdsEndpointActive)
//                {
//                    GlobalLog.WriteToStringBuilderLog($"MmaEndpointActive or OdsEndpointActive in config have to be active in{configurationFile}.", 14003);
//                    return;
//                }

//                // Default mode is token Authentication
//                OdsAuthenticationMode odsAuthenticationMode = OdsAuthenticationMode.Token;
//                if (odsPrivateApiConfig.MmaEndpointActive)
//                {
//                    odsAuthenticationMode = OdsAuthenticationMode.Certificate;
//                }

//                X509Certificate2 authX509Certificate = odsAuthenticationMode == OdsAuthenticationMode.Token ? FindCertificateByName(odsPrivateApiConfig.AadAppIdAuthCertificate) : FindCertificateThumbPrintByWorkspaceId(odsPrivateApiConfig.WorkspaceId);

//                // If no certificate is present, exit the application.
//                if (authX509Certificate == null)
//                {
//                    GlobalLog. WriteToStringBuilderLog("All tasks completed");
//                    Environment.Exit(0);
//                }

//                GlobalLog. WriteToStringBuilderLog($"Private Key KeyExchangeAlgorithm: {authX509Certificate.PrivateKey.KeyExchangeAlgorithm}");
//                GlobalLog. WriteToStringBuilderLog($"Private Key SignatureAlgorithm: {authX509Certificate.PrivateKey.SignatureAlgorithm}");

//                if (!Environment.UserName.Contains("rbiles"))
//                {
//                    GlobalLog. WriteToStringBuilderLog($"Shutting down...");
//                    return;
//                }

//                // Get the data from the sample blob
//                var jsonData = ReadSampleBlobToWindowsEventJsonList();

//                var odsHttpEndPointConnectorContext = new OdsHttpEndPointConnectorContext(
//                    odsPrivateApiConfig.ConnectorName,
//                    odsPrivateApiConfig.OdsEnpointUri,
//                    odsPrivateApiConfig.ResourceName,
//                    odsPrivateApiConfig.AadApplicationId,
//                    odsPrivateApiConfig.AadApplicationAuthUrl,
//                    odsPrivateApiConfig.AadAuthorityUrl,
//                    odsPrivateApiConfig.UploadShouldCompress,
//                    odsPrivateApiConfig.UploadBatchSize,
//                    authX509Certificate,
//                    authX509Certificate,
//                    odsPrivateApiConfig.EndpointRetryPolicy,
//                    odsAuthenticationMode);

//                var odsHttpEndPointStringConnector =
//                    new OdsHttpEndPointStringConnector(odsHttpEndPointConnectorContext);

//                Dictionary<string, object> workspaceDictionary =
//                    new Dictionary<string, object> { { "WorkspaceId", odsPrivateApiConfig.WorkspaceId }, { "WorkflowName", "SECURITY_WEF_EVENT_BLOB" } };

//                for (int i = 0; i < 1; i++)
//                {
//                    Task result = odsHttpEndPointStringConnector.SendAsync(jsonData, new CancellationToken(), workspaceDictionary);

//                    Task.WaitAll(result);

//                    GlobalLog. WriteToStringBuilderLog($"Finished writing: {i}");
//                }

//                GlobalLog. WriteToStringBuilderLog("All tasks completed");
//            }
//            catch (Exception e)
//            {
//                GlobalLog. WriteToStringBuilderLog(e.ToString(), 14005);
//            }
//        }

//        private static List<string> ReadSampleBlobToWindowsEventList()
//        {
//            List<string> returnJsonList = new List<string>();

//            try
//            {
//                var path = Assembly.GetExecutingAssembly().Location;
//                var directory = Path.GetDirectoryName(path);

//                // WARNING: The LA/Sentinel workflow AUTOMATICALLY ignores any event who's TimeCreated is greater than 14 days old!!!!
//                string txtSampleBlobFilePath = Path.Combine(directory, "ExampleWefMgmtPackBlob.txt");

//                List<string> windowsEventsFromBlob = File.ReadAllLines(txtSampleBlobFilePath).ToList();

//                foreach (string s in windowsEventsFromBlob)
//                {
//                    returnJsonList.Add(s);

//                    List<string> windowsEventElements = s.Split(new[] { "," }, StringSplitOptions.None).ToList();

//                    GlobalLog. WriteToStringBuilderLog($"ItemCount: {windowsEventElements.Count}");

//                    WindowsEventBlobMap tempWindowsEvent = new WindowsEventBlobMap()
//                    {
//                        TimeGenerated = Convert.ToDateTime(windowsEventElements[0]),
//                        SourceHealthServiceId = new Guid(windowsEventElements[1]),
//                        EventOriginId = new Guid(windowsEventElements[2]),
//                        ProviderGUID = new Guid(windowsEventElements[3]),
//                        PublisherName = windowsEventElements[4],
//                        Provider = windowsEventElements[5],
//                        Channel = windowsEventElements[6],
//                        Computer = windowsEventElements[7],
//                        EventNumber = Convert.ToInt32(windowsEventElements[8]),
//                        Task = Convert.ToInt32(windowsEventElements[9]),
//                        EventLevel = Convert.ToInt32(windowsEventElements[10]),
//                        UserName = windowsEventElements[11],
//                        Message = windowsEventElements[12],
//                        ParameterXml = windowsEventElements[13],
//                        Data = windowsEventElements[14],
//                        EventID = Convert.ToInt32(windowsEventElements[15]),
//                        RenderingDescription = windowsEventElements[16],
//                        ManagedEntityId = new Guid(windowsEventElements[17]),
//                        RuleId = new Guid(windowsEventElements[18]),
//                        Mg = new Guid(windowsEventElements[19]),
//                        TimeCollected = Convert.ToDateTime(windowsEventElements[20]),
//                        ManagementGroupName = windowsEventElements[21],
//                        LocalIpAddress = windowsEventElements[22],
//                        UnknownValue5 = windowsEventElements[23],
//                        UnknownValue6 = windowsEventElements[24],
//                        Guid7 = new Guid(windowsEventElements[25]),
//                    };

//                    string tempWinEvent = tempWindowsEvent.ToString();
//                    if (s.Equals(tempWinEvent))
//                    {
//                        GlobalLog. WriteToStringBuilderLog($"String not equal!!!!!!!!!!!!!", 14012);
//                        GlobalLog. WriteToStringBuilderLog($"Original: {s}");
//                        GlobalLog. WriteToStringBuilderLog($"Hydrated: {tempWinEvent}");
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                GlobalLog. WriteToStringBuilderLog(e.ToString(), 14006);
//            }

//            return returnJsonList;
//        }


//        private static List<string> ReadSampleBlobToWindowsEventJsonList()
//        {
//            List<string> returnJsonList = new List<string>();
//            int recordCounter = 0;

//            try
//            {
//                var path = Assembly.GetExecutingAssembly().Location;
//                var directory = Path.GetDirectoryName(path);

//                // WARNING: The LA/Sentinel workflow AUTOMATICALLY ignores any event who's TimeCreated is greater than 14 days old!!!!
//                string txtSampleBlobFilePath = Path.Combine(directory, "ExampleWefMgmtPackBlob.txt");

//                List<string> windowsEventsFromBlob = File.ReadAllLines(txtSampleBlobFilePath).ToList();

//                foreach (string s in windowsEventsFromBlob)
//                {
//                    List<string> windowsEventElements = s.Split(new[] { "," }, StringSplitOptions.None).ToList();

//                    GlobalLog. WriteToStringBuilderLog($"ItemCount: {windowsEventElements.Count}");

//                    string eventDataSample =
//                        "<DataItem type=\"System.XmlData\" time=\"2019-08-08T14:20:58.0130787+00:00\" sourceHealthServiceId=\"7896DB24-81DF-2EAB-B12B-29AED6D69A17\"><EventData xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"><Data Name=\"SubjectUserSid\">S-1-5-18</Data><Data Name=\"SubjectUserName\">RussellPhantom$</Data><Data Name=\"SubjectDomainName\">GME</Data><Data Name=\"SubjectLogonId\">0x3e7</Data><Data Name=\"NewProcessId\">0xec0</Data><Data Name=\"NewProcessName\">C:\\Windows\\System32\\conhost.exe</Data><Data Name=\"TokenElevationType\">%%1936</Data><Data Name=\"ProcessId\">0xc5c</Data><Data Name=\"CommandLine\">\\??\\C:\\Windows\\system32\\conhost.exe 0xffffffff -ForceV1</Data><Data Name=\"TargetUserSid\">S-1-0-0</Data><Data Name=\"TargetUserName\">-</Data><Data Name=\"TargetDomainName\">-</Data><Data Name=\"TargetLogonId\">0x0</Data><Data Name=\"ParentProcessName\">C:\\Windows\\System32\\cscript.exe</Data><Data Name=\"MandatoryLabel\">S-1-16-16384</Data></EventData></DataItem>";

//                    XDocument xml = XDocument.Parse(eventDataSample);


//                    WindowsEventBlobMap tempWindowsEvent = new WindowsEventBlobMap()
//                    {
//                        TimeGenerated = Convert.ToDateTime(windowsEventElements[0]),
//                        SourceHealthServiceId = new Guid(windowsEventElements[1]),
//                        EventOriginId = new Guid(windowsEventElements[2]),
//                        ProviderGUID = new Guid(windowsEventElements[3]),
//                        PublisherName = windowsEventElements[4],
//                        Provider = windowsEventElements[5],
//                        Channel = windowsEventElements[6],
//                        Computer = windowsEventElements[7],
//                        EventNumber = Convert.ToInt32(windowsEventElements[8]),
//                        Task = Convert.ToInt32(windowsEventElements[9]),
//                        EventLevel = Convert.ToInt32(windowsEventElements[10]),
//                        UserName = windowsEventElements[11],
//                        Message = windowsEventElements[12],
//                        ParameterXml = windowsEventElements[13],
//                        Data = eventDataSample,
//                        EventID = Convert.ToInt32(windowsEventElements[15]),
//                        RenderingDescription = windowsEventElements[16],
//                        ManagedEntityId = new Guid(windowsEventElements[17]),
//                        RuleId = new Guid(windowsEventElements[18]),
//                        Mg = new Guid(windowsEventElements[19]),
//                        TimeCollected = Convert.ToDateTime(windowsEventElements[20]),
//                        ManagementGroupName = windowsEventElements[21],
//                        LocalIpAddress = windowsEventElements[22],
//                        UnknownValue5 = windowsEventElements[23],
//                        UnknownValue6 = windowsEventElements[24],
//                        Guid7 = new Guid(windowsEventElements[25]),
//                    };

//                    var serializedEventRecord = JsonConvert.SerializeObject(tempWindowsEvent, new JsonSerializerSettings
//                    {
//                        TypeNameHandling = TypeNameHandling.Auto
//                    });
//                    returnJsonList.Add(serializedEventRecord);
//                }
//            }
//            catch (Exception e)
//            {
//                GlobalLog. WriteToStringBuilderLog(e.ToString(), 14007);
//            }

//            return returnJsonList;
//        }

//        public static string GetLocalIPAddress()
//        {
//            string ipAddress = "0.0.0.0";
//            try
//            {
//                var host = Dns.GetHostEntry(Dns.GetHostName());
//                foreach (var ip in host.AddressList)
//                {
//                    if (ip.AddressFamily == AddressFamily.InterNetwork)
//                    {
//                        ipAddress = ip.ToString();
//                    }
//                }

//                return ipAddress;
//            }
//            catch (Exception e)
//            {
//                return "0.0.0.0";
//            }
//        }

//        private static X509Certificate2 FindCertificateThumbPrintByWorkspaceId(string workspaceId)
//        {
//            string returnThumbprint = string.Empty;

//            try
//            {
//                string workspaceRegistryKeyPath =
//                    $"SYSTEM\\CurrentControlSet\\Services\\HealthService\\Parameters\\Service Connector Services\\Log Analytics - {workspaceId}";

//                RegistryKey key = Registry.LocalMachine.OpenSubKey(workspaceRegistryKeyPath);

//                Object regKeyValue = key.GetValue("Authentication Certificate Thumbprint");
//                if (regKeyValue != null)
//                {
//                    //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
//                    returnThumbprint = regKeyValue as string;

//                    returnThumbprint = new string(returnThumbprint.Where(c => !char.IsControl(c)).ToArray());
//                }

//                return FindCertificateByThumbprint(returnThumbprint);
//            }
//            catch (Exception e)
//            {
//                GlobalLog. WriteToStringBuilderLog(e.ToString(), 14010);
//                throw;
//            }
//        }

//        private static X509Certificate2 FindCertificateByThumbprint(string thumbprint)
//        {
//            X509Certificate2 returnX509Certificate2 = null;

//            try
//            {
//                var certStore = new X509Store("Microsoft Monitoring Agent", StoreLocation.LocalMachine);
//                // Try to open the store.

//                certStore.Open(OpenFlags.MaxAllowed);
//                // Find the certificate that matches the thumbprint.
//                var certCollection = certStore.Certificates.Find(
//                    X509FindType.FindByThumbprint, thumbprint, false);
//                certStore.Close();

//                // Check to see if our certificate was added to the collection. If no, 
//                // throw an error, if yes, create a certificate using it.
//                if (certCollection.Count >= 1)
//                {
//                    GlobalLog. WriteToStringBuilderLog($"Certificate found for the workspaceId [{thumbprint}]");
//                    returnX509Certificate2 = certCollection[0];
//                }
//            }
//            catch (Exception e)
//            {
//                GlobalLog. WriteToStringBuilderLog(e.ToString(), 14009);
//                throw;
//            }

//            return returnX509Certificate2;
//        }

//        private static X509Certificate2 FindCertificateByName(string certName)
//        {
//            X509Certificate2 returnX509Certificate2 = null;

//            var store = new X509Store("MY", StoreLocation.LocalMachine);
//            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

//            var collection = store.Certificates;

//            var certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
//            // Try to open the store.

//            certStore.Open(OpenFlags.ReadOnly);
//            // Find the certificate that matches the thumbprint.
//            var certCollection = certStore.Certificates.Find(
//                X509FindType.FindBySubjectName, certName, false);
//            certStore.Close();

//            // Check to see if our certificate was added to the collection. If no, 
//            // throw an error, if yes, create a certificate using it.
//            if (certCollection.Count >= 1)
//            {
//                GlobalLog. WriteToStringBuilderLog($"Certificate found containing name [{certName}]");
//                returnX509Certificate2 = certCollection[0];
//            }

//            return returnX509Certificate2;
//        }
//    }
//}