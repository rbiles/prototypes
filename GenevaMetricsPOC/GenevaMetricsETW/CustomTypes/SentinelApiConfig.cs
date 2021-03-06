﻿// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using GenevaETW.API;
using GenevaETW.API.CustomTypes;

namespace GenevaEtwPOC.CustomTypes
{
    public class SentinelApiConfig
    {
        public string WorkspaceId { get; set; }

        public string OdsEndpointUri { get; set; }

        public string CertificateThumbprint { get; set; }

        public string ManagementGroupId { get; set; }

        public bool UseMmaCertificate { get; set; } = true;

        public string AgentGuid { get; set; }

        public int MaxIngestorCount { get; set; } = 20;

        public int EventIngestBatchSize { get; set; } = 200;

        public int MaxItemLingerTime { get; set; } = 1000;

        public string DataType { get; set; }

        public string IpName { get; set; }

        public GenevaMdmConfiguration SloMetricsConfiguration { get; set; }
    }
}