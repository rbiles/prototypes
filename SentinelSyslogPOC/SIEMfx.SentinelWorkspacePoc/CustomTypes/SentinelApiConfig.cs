// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.CustomTypes
{
    using System;

    public class SentinelApiConfig
    {
        public string WorkspaceId { get; set; }

        public string OdsEndpointUri { get; set; }

        public string OmsEndpointUri { get; set; }

        public string CertificateThumbprint { get; set; }

        public string ManagementGroupId { get; set; }

        public bool UseMmaCertificate { get; set; } = true;

        public string AgentGuid { get; set; }

        public int MaxIngestorCount { get; set; } = 20;

        public int EventIngestBatchSize { get; set; } = 200;

        public int MaxItemLingerTime { get; set; } = 1000;

        public string DataType { get; set; }

        public string LogName { get; set; }

        public string IpName { get; set; }

        public string KeyVaultUri { get; set; }

        public bool UseKeyVaultForCertificates { get; set; }

        public string SyslogToAzureBlobStorageSecret { get; set; }

        public bool StoreDataToBlobStorage { get; set; }

        public KustoDataSourceConfig KustoDataSourceConfig { get; set; }

        public EnabledSentinelUploads EnabledSentinelUploads { get; set; }
    }
}