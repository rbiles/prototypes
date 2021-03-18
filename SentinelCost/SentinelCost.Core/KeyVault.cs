// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/


namespace SentinelCost.Core
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.KeyVault;

    public class KeyVault : IVault
    {
        private readonly AuthenticationCallbacks authenticationCallbacks;

        private readonly Func<string, string> cacheSecretSetting;

        private KeyVaultInfo keyVaultInfo;

        public KeyVault(KeyVaultInfo keyVaultInfo)
        {
            // configurationBase = new ConfigurationBase();
            cacheSecretSetting = MemoizationExtensions.Memoize<string, string>(InternalGetSecret);

            this.keyVaultInfo = keyVaultInfo;

            authenticationCallbacks = new AuthenticationCallbacks(keyVaultInfo);
        }

        public string GetSecret(string secretName)
        {
            return cacheSecretSetting(secretName);
        }

        public X509Certificate2 GetCertificate(string certificateName)
        {
            string keyVaultUri = keyVaultInfo.KeyVaultUri;

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

        private string InternalGetSecret(string secretName)
        {
            string keyVaultUri = keyVaultInfo.KeyVaultUri;

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
    }
}