// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace GenevaETW.API.CustomTypes
{
    public class GenevaMdmConfiguration
    {
        public string MetricsNamespace { get; set; }

        public string MetricsAccount { get; set; }

        public string LocationId { get; set; }

        public int MinimumValue { get; set; }

        public int BucketSize { get; set; }

        public ushort BucketCount { get; set; }
    }
}