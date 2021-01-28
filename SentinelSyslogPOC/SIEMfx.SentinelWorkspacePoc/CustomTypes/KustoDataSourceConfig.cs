// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.CustomTypes
{
    using System;

    public class KustoDataSourceConfig
    {
        public string ClusterUri { get; set; }

        public string Database { get; set; }
    }
}