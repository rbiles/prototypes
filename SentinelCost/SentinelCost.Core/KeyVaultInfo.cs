// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using System.Security.Cryptography.X509Certificates;

    public class KeyVaultInfo
    {
        public bool UseCertAuth { get; set; } = true;

        public string CertThumbprint { get; set; }

        public string ClientAppId { get; set; }

        public string ClientSecret { get; set; }

        public string KeyVaultUri { get; set; }

        public bool UseAzureIdentity { get; set; } = false;

        public StoreLocation CertThumbprintLocation { get; set; }

        public bool UseSecretKey { get; set; }

        public KeyVaultInfo()
        {

        }

        public KeyVaultInfo(KeyVaultInfo other)
        {
            CertThumbprint = other.CertThumbprint;
            CertThumbprintLocation = other.CertThumbprintLocation;
            ClientAppId = other.ClientAppId;
            ClientSecret = other.ClientSecret;
            KeyVaultUri = other.KeyVaultUri;
            UseCertAuth = other.UseCertAuth;
        }
    }
}
