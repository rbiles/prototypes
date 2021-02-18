using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GenevaEtwPOC
{
    public class CertificateManagement
    {
        public static X509Certificate2 FindOdsCertificateByWorkspaceId(string workspaceId)
        {
            string returnThumbprint = String.Empty;

            // Path to the GUID for the current and valid certificate on the LA Workspace
            string workspaceRegistryKeyPath =
                $@"SYSTEM\CurrentControlSet\Services\HealthService\Parameters\Service Connector Services\Log Analytics - {workspaceId}";

            RegistryKey key = Registry.LocalMachine.OpenSubKey(workspaceRegistryKeyPath);

            Object regKeyValue = key.GetValue("Authentication Certificate Thumbprint");
            if (regKeyValue != null)
            {
                //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
                returnThumbprint = regKeyValue as string;

                returnThumbprint = new string(returnThumbprint.Where(c => !Char.IsControl(c)).ToArray());
            }

            // Retrieve the current workspace certificate
            return FindCertificateByThumbprint("Microsoft Monitoring Agent", returnThumbprint, StoreLocation.LocalMachine);
        }

        public static X509Certificate2 FindCertificateByThumbprint(string storeName, string thumbprint, StoreLocation storeLocation)
        {
            X509Certificate2 returnX509Certificate = null;

            var certStore = new X509Store(storeName, storeLocation);
            // Try to open the store.

            certStore.Open(OpenFlags.MaxAllowed);
            // Find the certificate that matches the thumbprint.
            var certCollection = certStore.Certificates.Find(
                X509FindType.FindByThumbprint, thumbprint, false);
            certStore.Close();

            // Check to see if our certificate was added to the collection. If no, 
            // throw an error, if yes, create a certificate using it.
            if (certCollection.Count >= 1)
            {
                returnX509Certificate = certCollection[0];
            }

            return returnX509Certificate;
        }

        public static X509Certificate2 FindCertificateByName(string certName)
        {
            X509Certificate2 returnX509Certificate2 = null;

            var store = new X509Store("MY", StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            var collection = store.Certificates;

            var certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            // Try to open the store.

            certStore.Open(OpenFlags.MaxAllowed);
            // Find the certificate that matches the thumbprint.
            var certCollection = certStore.Certificates.Find(
                X509FindType.FindBySubjectName, certName, false);
            certStore.Close();

            // Check to see if our certificate was added to the collection. If no, 
            // throw an error, if yes, create a certificate using it.
            if (certCollection.Count >= 1)
            {
                returnX509Certificate2 = certCollection[0];
            }

            return returnX509Certificate2;
        }

        public static void RegisterWithOms(string thumbprint, string agentGuid, string workspaceId, string workspaceKey, string environmentRootUri)
        {
            X509Certificate2 cert = CertificateManagement.FindCertificateByThumbprint("My", thumbprint, StoreLocation.LocalMachine);
            string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary

            string date = DateTime.Now.ToString("O");
            string xmlContent = "<?xml version=\"1.0\"?><AgentTopologyRequest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/\"><FullyQualfiedDomainName>sagebree-dev.redmond.corp.microsoft.com</FullyQualfiedDomainName><EntityTypeId>"
                + agentGuid
                + "</EntityTypeId><AuthenticationCertificate>"
                + rawCert
                + "</AuthenticationCertificate></AgentTopologyRequest>";

            SHA256 sha256 = SHA256.Create();
            string contentHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.ASCII.GetBytes(xmlContent)));

            // AuthKey = SHA256(HMAC(ContentHash, Key));
            string authKey = String.Format("{0}; {1}", workspaceId, Sign(date, contentHash, workspaceKey));

            try
            {
                WebRequestHandler clientHandler = new WebRequestHandler();
                clientHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(clientHandler);

                string url = $"https://{workspaceId}.{environmentRootUri}/AgentService.svc/AgentTopologyRequest";

                client.DefaultRequestHeaders.Add("x-ms-Date", date);
                client.DefaultRequestHeaders.Add("x-ms-version", "August, 2014");
                client.DefaultRequestHeaders.Add("x-ms-SHA256_Content", contentHash);
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authKey);
                client.DefaultRequestHeaders.Add("user-agent", "MonitoringAgent/OneAgent");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US");

                HttpContent httpContent = new StringContent(xmlContent, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
                Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);


                HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine("Return Result: " + result);
                Console.WriteLine(response.Result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }

        private static string Sign(string requestdate, string contenthash, string key)
        {
            StringBuilder signatureBuilder = new StringBuilder();
            signatureBuilder.Append(requestdate);
            signatureBuilder.Append("\n");
            signatureBuilder.Append(contenthash);
            signatureBuilder.Append("\n");
            string rawsignature = signatureBuilder.ToString();

            //string rawsignature = contenthash;

            HMACSHA256 hKey = new HMACSHA256(Convert.FromBase64String(key));
            return Convert.ToBase64String(hKey.ComputeHash(Encoding.UTF8.GetBytes(rawsignature)));
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        public static string HMACSHA256(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            //byte[] keyByte = Encoding.ASCII.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                //return encoding.GetString(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static string GetHmacAndBase64(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            //byte[] keyByte = Encoding.ASCII.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /*
        public static void GetConfigFileFromOms()
        {
            const int CALG_AES_256 = 0x00006610;
            X509Certificate2 cert = Find(StoreLocation.CurrentUser, MyThumbprint);
            string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary

            // ConfigurationService.Svc/GetConfigurationFile

            string configFileRequest = "<?xml version=\"1.0\"?><ConfigurationFileRequest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" healthServiceId=\""
            + AgentGuid
                + "\" managementGroupId=\""
                + "110941af-233e-58b8-a186-438212380b0a"
                + "\"><PublicKey>"
                + rawCert
                + "</PublicKey><RequestedEncryptionAlgorithm>"
                + CALG_AES_256
                + "</RequestedEncryptionAlgorithm></ConfigurationFileRequest>";

            try
            {
                WebRequestHandler clientHandler = new WebRequestHandler();
                clientHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(clientHandler);

                string url = "https://" + WorkspaceId + ".oms.opinsights.azure.com/ConfigurationService.Svc/GetConfigurationFile";


                //client.DefaultRequestHeaders.Add("x-ms-Date", DateTime.Now.ToString("O"));
                //client.DefaultRequestHeaders.Add("x-ms-version", "August, 2014");

                //client.DefaultRequestHeaders.Add("user-agent", "MonitoringAgent/OneAgent");
                //client.DefaultRequestHeaders.Add("Accept-Language", "en-US");


                System.Net.Http.HttpContent httpContent = new StringContent(req, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);


                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine("Return Result: " + result);
                Console.WriteLine(response.Result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
        */
    }
}
