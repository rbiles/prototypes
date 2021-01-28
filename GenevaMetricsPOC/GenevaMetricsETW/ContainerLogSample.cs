using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalyticsOdsApiHarness
{
    public class ContainerLogSample
    {
        //TODO: generate cert using
        // New-SelfSignedCertificate -KeyLength 2048 -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -HashAlgorithm Sha1 -Subject "O=Microsoft;OU=Microsoft Monitoring Agent;CN={<AgentGuid>};CN={<WorkspaceId>}\"
        // https://msazure.visualstudio.com/One/_git/Mgmt-LogAnalytics-OMS?path=%2Fsrc%2FTest%2FCertBase64%2FProgram.cs&version=GBmaster&line=163&lineStyle=plain&lineEnd=165&lineStartColumn=1&lineEndColumn=84

        static string MyThumbprint = "47FF8048010185FAC2A1056B1C9282218F5E74E7".ToUpper(); // generated using c++
        static string AgentGuid = "d0bd5ba9-c7b6-44f0-8e3d-9bfb595038c7";
        static string WorkspaceId = "834693e0-2d47-4777-91db-31288c483532";
        static string WorkspaceKey = "";
        private static bool UseMmaCertificate = true;

        public static void InsertSampleDataSet()
        {
            //testing..
            //string fileName = null;
            //string IaaSRcfFileLocation = @"C:\Users\sagebree\Desktop\RcfFiles";

            //DirectoryInfo rcfDirectory = new DirectoryInfo(IaaSRcfFileLocation);
            //FileInfo rcfFile = rcfDirectory.GetFiles("*.xml", SearchOption.TopDirectoryOnly)
            //    .OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault();

            //if (rcfFile != null)
            //{
            //    fileName = rcfFile.FullName;
            //}
            //testing..

            // RegisterWithOms();

            SendDataToODS_ContainerLog(UseMmaCertificate);
        }

        public static void SendDataToODS_ContainerLog(bool useMmaCert)
        {
            X509Certificate2 cert = null;
            if (useMmaCert)
            {
                cert = CertificateManagement.FindOdsCertificateByWorkspaceId(WorkspaceId);
            }
            else
            {
                cert = Find(StoreLocation.LocalMachine, MyThumbprint);
            }


            // string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary
            string requestId = Guid.NewGuid().ToString("D");
            string jsonContent = File.ReadAllText("ContainerLog.json");

            string dateTime = DateTime.Now.ToString("O");

            try
            {
                WebRequestHandler clientHandler = new WebRequestHandler();
                clientHandler.ClientCertificates.Add(cert);
                var client = new HttpClient(clientHandler);

                string url = "https://" + WorkspaceId + ".ods.opinsights.azure.com/OperationalData.svc/PostJsonDataItems?api-version=2016-04-01";
                client.DefaultRequestHeaders.Add("X-Request-ID", requestId);

                System.Net.Http.HttpContent httpContent = new StringContent(jsonContent, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
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

        private static X509Certificate2 Find(StoreLocation location, string thumbprint)
        {
            X509Certificate2 cert = null;
            X509Store store = new X509Store(location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var c in store.Certificates)
            {
                if (c.Thumbprint == MyThumbprint)
                {
                    cert = c;
                    break;
                }
                Console.WriteLine(c.Subject);
            }
            //IEnumerable certs = store.Certificates.Find(X509FindType.findbysu, thumbprint, true);
            //var cert = certs.OfType<X509Certificate>().FirstOrDefault();
            store.Close();
            return cert;
        }
    }
}
