// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    public enum SinkType
    {
        LogAnalytics,
        Kusto,
        ArisV5,
        ArisV6,
        LiveStream,
        CdiEventHub,
        KustoSecurityLog, // Specific sink, output goes to SecurityLog table.
        KustoArisAlerts, // Specific sink, output goes to v5Alerts table.
    }

    /// <summary>Encodes KeyVault authentication mode. </summary>
    public enum KeyVaultAuthenticationMode
    {
        /// <summary>Use local certificate. </summary>
        Certificate,

        /// <summary>Obtain a token for use in Azure based on the current contextual service in Azure (IaaS, PaaS, etc...) </summary>
        AzureToken
    }

    public class SinkSettings
    {
        public string SinkSecretKey { get; set; }

        public string EndpointUrl { get; set; }

        public string SinkId { get; set; }

        public string Database { get; set; }

        public string Table { get; set; }

        public string Authority { get; set; }

        public bool UseAzureIdentity { get; set; }

        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public class EventSinkInfo
    {
        public string SinkAlias { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SinkType SinkType { get; set; }

        public SinkSettings Settings { get; set; }
    }
}