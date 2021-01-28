// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using SIEMfx.SentinelWorkspacePoc.CustomTypes;

    public class KeyVault : IVault
    {
        private readonly AuthenticationCallbacks authenticationCallbacks;

        private readonly Func<string, string> cacheSecretSetting;

        private readonly SentinelApiConfig sentinelApiConfig;

        public KeyVault(SentinelApiConfig sentinelApiConfig)
        {
            // configurationBase = new ConfigurationBase();
            this.cacheSecretSetting = MemoizationExtensions.Memoize<string, string>(InternalGetSecret);

            this.sentinelApiConfig = sentinelApiConfig;

            this.authenticationCallbacks = new AuthenticationCallbacks(sentinelApiConfig);
        }

        public string GetSecret(string secretName)
        {
            return cacheSecretSetting(secretName);
        }

        public X509Certificate2 GetCertificate(string certificateName)
        {
            string keyVaultUri = sentinelApiConfig.KeyVaultUri;

            try
            {
                // Craft the certificate URI
                var secretUri = new UriBuilder
                {
                    Host = keyVaultUri,
                    Scheme = "https"
                }.Uri;

                var kv = new KeyVaultClient(authenticationCallbacks.GetToken);

                var certificateBundle = kv.GetCertificateAsync(secretUri.ToString(), certificateName).GetAwaiter().GetResult();
                var certificateSecret = kv.GetSecretAsync(certificateBundle.SecretIdentifier.Identifier);
                byte[] certificateDecoded = Convert.FromBase64String(certificateSecret.Result.Value);
                return new X509Certificate2(certificateDecoded, string.Empty);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error getting certificate <{certificateName}> from the KeyVault", ex);
            }
        }

        public async Task<bool> StoreCertSecret(string secretName, string secretValue)
        {
            string keyVaultUri = sentinelApiConfig.KeyVaultUri;

            try
            {
                var kv = new KeyVaultClient(authenticationCallbacks.GetToken);
                // Craft the certificate URI

                var secretUri = new UriBuilder
                {
                    Host = keyVaultUri,
                    Scheme = "https"
                }.Uri;

                SecretAttributes attribs = new SecretAttributes
                {
                    Enabled = true,
                    Expires = DateTime.UtcNow.AddDays(90), // if you want to expire the info
                    //NotBefore = DateTime.UtcNow.AddDays(1) // if you want the info to 
                };

                IDictionary<string, string> alltags = new Dictionary<string, string>();
                alltags.Add("CreationDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"));
                alltags.Add("MachineName", Environment.MachineName);
                string contentType = "SentinelWSAuth"; // whatever you want to categorize it by; you name it

                SecretBundle bundle = await kv.SetSecretAsync
                    (secretUri.AbsoluteUri, secretName, secretValue, alltags, contentType, attribs);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                throw new ApplicationException($"Error storing secret <{secretName}> to the KeyVault {sentinelApiConfig.KeyVaultUri}", ex);
            }
        }

        private string InternalGetSecret(string secretName)
        {
            string keyVaultUri = sentinelApiConfig.KeyVaultUri;

            // string keyVaultUri = configurationBase.GetAppSetting(KeyVaultUriKey);
            Uri secretUri = new UriBuilder
            {
                Host = keyVaultUri,
                Path = $"{"secrets"}/{secretName}",
                Scheme = "https"
            }.Uri;

            var kv = new KeyVaultClient(authenticationCallbacks.GetToken);
            return kv.GetSecretAsync(secretUri.AbsoluteUri).Result.Value;
        }

        public bool Equals(IVault other)
        {
            var kv = other as KeyVault;

            return true;
        }
    }
}