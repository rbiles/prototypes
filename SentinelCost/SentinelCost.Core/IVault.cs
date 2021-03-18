// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using System.Security.Cryptography.X509Certificates;

    public interface IVault
    {
        string GetSecret(string secretName);

        X509Certificate2 GetCertificate(string certificateName);
    }
}
