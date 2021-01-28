// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SIEMfx.SentinelWorkspaceApi
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Microsoft.Win32;

    public class CertificateManagement : IDisposable
    {
        public bool SaveCertificateToStore(X509Certificate2 certificate, string storeName, StoreLocation location)
        {
            try
            {
                using (X509Store store = new X509Store(storeName, location))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(certificate);
                    store.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        public bool RemoveCertificateFromStore(string thumbprint)
        {
            return true;
        }

        public X509Certificate2 CreateOmsSelfSignedCertificate(string agentId, string workspaceId)
        {
            string subjectName = $"O=Microsoft;OU=Microsoft Monitoring Agent;CN={agentId};DC={workspaceId}";

            X500DistinguishedName distinguishedName = new X500DistinguishedName(subjectName);

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.PublicKey.Oid.FriendlyName = $"Microsoft Monitoring Agent - {workspaceId}";
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));

                var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(90)));

                certificate.FriendlyName = $"Microsoft Monitoring Agent - {workspaceId}";

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx), string.Empty, X509KeyStorageFlags.MachineKeySet);
            }
        }

        public X509Certificate2 FindOdsCertificateByWorkspaceId(string workspaceId)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public X509Certificate2 FindCertificateByThumbprint(string storeName, string thumbprint, StoreLocation storeLocation)
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

        public X509Certificate2 FindCertificateByName(string storeName, string certName, StoreLocation storeLocation)
        {
            try
            {
                X509Certificate2 returnX509Certificate2 = null;

                var store = new X509Store(storeName, storeLocation);
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public void RegisterWithOms(X509Certificate2 cert, string agentGuid, string workspaceId, string workspaceKey, string environmentRootUri)
        {
            // X509Certificate2 cert = CertificateManagement.FindCertificateByThumbprint("My", thumbprint, StoreLocation.LocalMachine);
            string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary

            string date = DateTime.Now.ToString("O");
            string xmlContent = "<?xml version=\"1.0\"?><AgentTopologyRequest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/\"><FullyQualfiedDomainName>russellhpdev.redmond.corp.microsoft.com</FullyQualfiedDomainName><EntityTypeId>"
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
                var mtlsHandler = new HttpClientHandler
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };

                //var httpClient = new HttpClient(mtlsHandler);
                //WebRequestHandler clientHandler = new WebRequestHandler();

                mtlsHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(mtlsHandler);

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
            catch (Exception ex)
            {
                Console.WriteLine("API Post Exception: " + ex.Message);
            }
        }


        public void RegisterWithOms(X509Certificate2 cert, string workspaceid, string workspacekey, string environmentUrl)
        {
            var uri = string.Format("https://{0}.{1}/AgentService.Svc/AgentTopologyRequest",
                workspaceid, environmentUrl);

            AgentTopologyRequest requestBody = new AgentTopologyRequest();
            requestBody.FullyQualfiedDomainName = "FakeAgent.domain.root";
            requestBody.EntityTypeId = "55270A70-AC47-C853-C617-236B0CFF9B4C";
            requestBody.AuthenticationCertificate = cert.RawData;
            var body = SerializeRequest(requestBody, typeof(AgentTopologyRequest));
            HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(uri);
            webrequest.Method = "Post";
            webrequest.ClientCertificates.Add(cert);
            UpdateAgentTopologyRequestDelegate(webrequest, body, workspaceid, workspacekey);

            try
            {
                var stream = webrequest.GetRequestStream();
                stream.Write(body, 0, body.Length);
                var response = webrequest.GetResponse();
            }
            catch (Exception ex)
            {
                WebException wex = (WebException)ex;
                var message = wex.Message;
                var exResponse = wex.Response;
                if (exResponse != null)
                {
                    var sr = new StreamReader(exResponse.GetResponseStream());
                    message = sr.ReadToEnd();
                }
                throw new Exception("Failed to register the cert with OMS: " + message);
            }
        }

        public void UpdateAgentTopologyRequestDelegate(HttpWebRequest webrequest, byte[] content, string workspaceid, string workspacekey)
        {
            string now = DateTime.Now.ToString("O");
            webrequest.Headers.Add("x-ms-Date", now);
            webrequest.Headers.Add("x-ms-version", "August, 2014");
            if (content != null)
            {
                SHA256 sha256 = SHA256.Create();
                string contentHash = Convert.ToBase64String(sha256.ComputeHash(content));
                webrequest.Headers.Add("x-ms-SHA256_Content", contentHash);
                webrequest.Headers.Add("Authorization", string.Format("{0}; {1}", workspaceid, Sign(now, contentHash, workspacekey)));
            }
        }

        public byte[] SerializeRequest(object requestobj, Type requesttype)
        {
            byte[] requestbytes = null;
            using (MemoryStream mstream = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(requesttype);
                serializer.Serialize(mstream, requestobj);
                requestbytes = mstream.ToArray();
            }
            return requestbytes;
        }

        private string Sign(string requestdate, string contenthash, string key)
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

        public byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        public string HMACSHA256(string message, string secret)
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

        public string GetHmacAndBase64(string message, string secret)
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

        public string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public string GetMachineFqdn()
        {
            //TODO: This should be moved somewhere common across SIEMfx common libraries
            string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName))
            {
                hostName += domainName;
            }

            return hostName;
        }

        public void Dispose()
        {
            // Placeholder for stray objects
        }
    }
}