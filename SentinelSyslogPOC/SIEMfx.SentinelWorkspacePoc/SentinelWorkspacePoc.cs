// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reactive.Kql;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Newtonsoft.Json;
    using SIEMfx.SentinelWorkspaceApi;
    using SIEMfx.SentinelWorkspacePoc.CustomTypes;
    using SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers;
    using Timer = System.Timers.Timer;

    public class SentinelWorkspacePoc : ServiceBase
    {
        private Timer HeartbeatTimer { get; set; }

        public SentinelApiConfig SentinelApiConfig { get; set; }

        public IVault KeyVault { get; set; }

        public SentinelWorkspacePoc()
        {
            // The constructor for the service
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(SentinelWorkspacePoc.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);

            // Turn on the KeyVault for use
            this.KeyVault = new KeyVault(SentinelApiConfig);

            // Use local certificate store, or KeyVault
            if (SentinelApiConfig.UseKeyVaultForCertificates)
            {
                ManageOdsAuthenticationKeyVault();
            }
            else
            {
                ManageOdsAuthenticationCertStore();
            }
        }

        public void ManageOdsAuthenticationCertStore()
        {
            try
            {
                string sentinalAuthWorkspaceKey = GetKeyVaultSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wskey");

                using (var certificateManagement = new CertificateManagement())
                {
                    var authX509Certificate2 = certificateManagement.FindCertificateByThumbprint("MY", SentinelApiConfig.CertificateThumbprint, StoreLocation.LocalMachine);

                    if (authX509Certificate2 == null)
                    {
                        string agentId = Guid.NewGuid().ToString("D");
                        authX509Certificate2 = certificateManagement.CreateOmsSelfSignedCertificate(agentId, SentinelApiConfig.WorkspaceId);

                        //TODO: Add in support for KeyVault
                        if (certificateManagement.SaveCertificateToStore(authX509Certificate2, "MY", StoreLocation.LocalMachine))
                        {
                            certificateManagement.RegisterWithOms(authX509Certificate2, SentinelApiConfig.WorkspaceId, sentinalAuthWorkspaceKey,
                                SentinelApiConfig.OmsEndpointUri);

                            SentinelApiConfig.CertificateThumbprint = authX509Certificate2.Thumbprint.ToLower();
                            SaveCurrentConfiguration();

                            authX509Certificate2 = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private string GetKeyVaultSecret(string secretName)
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

        public void ManageOdsAuthenticationKeyVault()
        {
            string sentinalAuthCertEncoded = GetKeyVaultSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wsid");
            string sentinalAuthWorkspaceKey = GetKeyVaultSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wskey");

            try
            {
                X509Certificate2 authX509Certificate2 = null;

                if (sentinalAuthCertEncoded == null)
                {
                    using (var certificateManagement = new CertificateManagement())
                    {
                        // Create a certificate to register with Oms
                        string agentId = Guid.NewGuid().ToString("D");
                        authX509Certificate2 = certificateManagement.CreateOmsSelfSignedCertificate(agentId, SentinelApiConfig.WorkspaceId);

                        // Register the certificate with Omc
                        if (certificateManagement.SaveCertificateToStore(authX509Certificate2, "MY", StoreLocation.LocalMachine))
                        {
                            certificateManagement.RegisterWithOms(authX509Certificate2, SentinelApiConfig.WorkspaceId, sentinalAuthWorkspaceKey,
                                SentinelApiConfig.OmsEndpointUri);

                            SentinelApiConfig.CertificateThumbprint = authX509Certificate2.Thumbprint.ToLower();
                            SaveCurrentConfiguration();
                        }

                        // From byte array to string
                        byte[] certByteArray = authX509Certificate2.GetRawCertData();
                        string certByteToStore = Encoding.Unicode.GetString(certByteArray, 0, certByteArray.Length);
                        var result = KeyVault.StoreCertSecret($"{SentinelApiConfig.WorkspaceId.ToLower()}-wsid", certByteToStore).ConfigureAwait(true);

                        var AuthX509Certificate2 = new X509Certificate2(certByteArray, string.Empty, X509KeyStorageFlags.Exportable);





                    }
                }
                else
                {
                    // From string to byte array
                    byte[] certFromKeyVault = Encoding.Unicode.GetBytes(sentinalAuthCertEncoded);

                    authX509Certificate2 = new X509Certificate2(certFromKeyVault, string.Empty, X509KeyStorageFlags.MachineKeySet);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SaveCurrentConfiguration()
        {
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];

            GlobalLog.WriteToStringBuilderLog($"Saving configuration file [{configurationFile}].", 14001);
            string textOfSentinelApiConfig = JsonConvert.SerializeObject(SentinelApiConfig, Formatting.Indented);
            File.WriteAllText(Path.Combine(SentinelWorkspacePoc.GetExecutionPath(), $"{configurationFile}"), textOfSentinelApiConfig);
        }

        protected override void OnStart(string[] args)
        {
            this.HeartbeatTimer = new Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = TimeSpan.FromSeconds(10).TotalMilliseconds
            };
            this.HeartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            this.HeartbeatTimer.Start();
        }

        private async void HeartbeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await this.ExecuteHeartbeatOperationAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                this.HeartbeatTimer.Start();
            }
        }

        public static void PrintCustomMessage(string message, ConsoleColor color)
        {
            ConsoleColor existing = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);

            //reset
            Console.ForegroundColor = existing;
        }

        private async Task ExecuteHeartbeatOperationAsync()
        {
            // Get the next batch of records to process 
            SentinelWorkspaceLogHub.GetNextBatchOfRecords();

            // The Container log sample upload code for JSON data
            if (SentinelApiConfig.EnabledSentinelUploads.WindowsEventsXmlFile)
            {
                SentinelWorkspaceLogHub.WindowsEventsXmlFile();
            }

            // The Container log sample upload code for JSON data
            if (SentinelApiConfig.EnabledSentinelUploads.WindowsEventsFolderContents)
            {
                SentinelWorkspaceLogHub.WindowsEventsFolderContents();
            }

            // The Container log sample upload code for JSON data
            if (SentinelApiConfig.EnabledSentinelUploads.LoadSecurityEventLog)
            {
                SentinelWorkspaceLogHub.LoadSecurityEventLog();
            }

            // The Container log sample upload code for JSON data
            if (SentinelApiConfig.EnabledSentinelUploads.SyslogToCustomLog)
            {
                await SentinelWorkspaceLogHub.SyslogToCustomLog();
            }

            // The SyslogToLinuxSyslog sample upload code for JSON data
            if (SentinelApiConfig.EnabledSentinelUploads.SyslogToLinuxSyslog)
            {
                await SentinelWorkspaceLogHub.SyslogToLinuxSyslogJson();
            }

            if (SentinelApiConfig.EnabledSentinelUploads.SyslogToCefSyslog)
            {
                await SentinelWorkspaceLogHub.SyslogToCefSyslogJson();
            }

            if (SentinelApiConfig.EnabledSentinelUploads.CefFilesToSentinelProcessor)
            {
                await SentinelWorkspaceLogHub.CefFilesToSentinelProcessor();
            }
        }

        protected override void OnStop()
        {
            // TODO: Cleanup any code or run shutdown logic
        }

        public void ManualStart(string[] args)
        {
            // TODO: This is called from the Program.cs file, when debugging or starting from a command line
            this.OnStart(args);
        }

        public void ManualStop()
        {
            // TODO: This is called from the Program.cs file, when debugging or stopping from a command line
            this.OnStop();
        }

        public static string GetExecutionPath()
        {
            // deserialize JSON to the runtime type, and iterate.
            var path = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(path);
            return directory;
        }
    }
}