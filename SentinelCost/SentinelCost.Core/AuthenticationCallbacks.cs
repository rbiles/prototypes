// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    internal class AuthenticationCallbacks
    {
        private readonly KeyVaultInfo keyVaultInfo;

        public AuthenticationCallbacks(KeyVaultInfo keyVaultInfo)
        {
            this.keyVaultInfo = keyVaultInfo;
        }

        public Task<string> GetToken(string authority, string resource, string scope)
        {
            if (keyVaultInfo.UseAzureIdentity)
            {
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
                return azureServiceTokenProvider.KeyVaultTokenCallback.Invoke(authority, resource, scope);
            }

            if (keyVaultInfo.UseSecretKey)
            {
                return GetKeyToken(authority, resource, scope);
            }
            else
            {
                return GetCertToken(authority, resource, scope);
            }
        }

        private async Task<string> GetKeyToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(keyVaultInfo.ClientAppId,
                keyVaultInfo.ClientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }

            return result.AccessToken;
        }

        private async Task<string> GetCertToken(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, AssertionCert);
            return result.AccessToken;
        }

        private ClientAssertionCertificate assertionCert;

        public ClientAssertionCertificate AssertionCert
        {
            get { return assertionCert ?? (assertionCert = GetCert()); }
        }

        private ClientAssertionCertificate GetCert()
        {
            try
            {
                StoreLocation storeLocation = StoreLocation.LocalMachine;

                var clientAssertionCertPfx =
                    FindCertificateByThumbprint(
                        keyVaultInfo.CertThumbprint, storeLocation);
                return new ClientAssertionCertificate(keyVaultInfo.ClientAppId,
                    clientAssertionCertPfx);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private X509Certificate2 FindCertificateByThumbprint(string findValue, StoreLocation storeLocation)
        {
            X509Store store = new X509Store(StoreName.My, storeLocation);

            try
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindByThumbprint, findValue, false);
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