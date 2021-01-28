// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using SIEMfx.SentinelWorkspacePoc.CustomTypes;

namespace SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers
{
    internal class AuthenticationCallbacks
    {
        private readonly SentinelApiConfig sentinelApiConfig;

        public AuthenticationCallbacks(SentinelApiConfig sentinelApiConfig)
        {
            this.sentinelApiConfig = sentinelApiConfig;
        }

        public Task<string> GetToken(string authority, string resource, string scope)
        {
            // Only use MSI, not for production use.
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            return azureServiceTokenProvider.KeyVaultTokenCallback.Invoke(authority, resource, scope);
        }

        private X509Certificate2 FindCertificateByThumbprint(string findValue, StoreLocation storeLocation)
        {
            var store = new X509Store(StoreName.My, storeLocation);

            try
            {
                store.Open(OpenFlags.ReadOnly);
                var col = store.Certificates.Find(X509FindType.FindByThumbprint, findValue, false);
                // Don't validate certs, since the test root isn't installed.
                return col.Count == 0 ? null : col[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}