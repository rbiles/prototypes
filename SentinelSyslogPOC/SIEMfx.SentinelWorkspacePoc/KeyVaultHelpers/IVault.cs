// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public interface IVault : IEquatable<IVault>
    {
        string GetSecret(string secretName);

        X509Certificate2 GetCertificate(string certificateName);

        Task<bool> StoreCertSecret(string secretName, string secretValue);
    }
}
