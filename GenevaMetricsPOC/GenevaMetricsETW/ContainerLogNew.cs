using Event.Ingest.Larp;
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
    using System.Collections;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ContainerLogNew
    {
        //TODO: generate cert using
        // New-SelfSignedCertificate -KeyLength 2048 -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -HashAlgorithm Sha1 -Subject "O=Microsoft;OU=Microsoft Monitoring Agent;CN={<AgentGuid>};CN={<WorkspaceId>}\"
        // https://msazure.visualstudio.com/One/_git/Mgmt-LogAnalytics-OMS?path=%2Fsrc%2FTest%2FCertBase64%2FProgram.cs&version=GBmaster&line=163&lineStyle=plain&lineEnd=165&lineStartColumn=1&lineEndColumn=84

        static string MyThumbprint = "47FF8048010185FAC2A1056B1C9282218F5E74E7".ToUpper(); // generated using c++
        static string WorkspaceId = "834693e0-2d47-4777-91db-31288c483532";
        private static bool UseMmaCertificate = true;

        public static void InsertSampleDataSet()
        {
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
            string jsonContent = File.ReadAllText("ContainerLogItems.json");

            var items = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(jsonContent);

            string dateTime = DateTime.Now.ToString("O");

            var config = new LarpUploaderConfig()
            {
                BatchSize = 100,
                MaxItemLingerTime = TimeSpan.FromMilliseconds(5000),
                WorkspaceId = WorkspaceId,
                JsonHeaderDataType = "CONTAINER_LOG_BLOB",
                JsonHeaderIPName = "logmanagement",
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
