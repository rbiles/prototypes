// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace SIEMfx.SentinelWorkspaceApi
{
    using System;
    using System.Diagnostics;
    using System.Xml.Serialization;

    /// <remarks />
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
    [Serializable()]
    [DebuggerStepThrough()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [XmlType(AnonymousType = true, Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/")]
    [XmlRoot(Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/", IsNullable = false)]
    public partial class AgentTopologyRequest
    {
        private string fullyQualfiedDomainNameField;

        private string entityTypeIdField;

        private byte[] authenticationCertificateField;

        private AgentTopologyRequestOperatingSystem operatingSystemField;

        /// <remarks />
        public string FullyQualfiedDomainName {
            get { return this.fullyQualfiedDomainNameField; }
            set { this.fullyQualfiedDomainNameField = value; }
        }

        /// <remarks />
        public string EntityTypeId {
            get { return this.entityTypeIdField; }
            set { this.entityTypeIdField = value; }
        }

        /// <remarks />
        [XmlElement(DataType = "base64Binary")]
        public byte[] AuthenticationCertificate {
            get { return this.authenticationCertificateField; }
            set { this.authenticationCertificateField = value; }
        }

        public AgentTopologyRequestOperatingSystem OperatingSystem {
            get { return this.operatingSystemField; }
            set { this.operatingSystemField = value; }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
    [Serializable()]
    [DebuggerStepThrough()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [XmlType(AnonymousType = true, Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/")]
    public partial class AgentTopologyRequestOperatingSystem
    {
        private string manufacturerField;

        private string nameField;

        private string versionField;

        private uint productTypeField;

        private AgentTopologyRequestOperatingSystemProcessorArchitecture processorArchitectureField;

        /// <remarks />
        public string Manufacturer {
            get { return this.manufacturerField; }
            set { this.manufacturerField = value; }
        }

        /// <remarks />
        public string Name {
            get { return this.nameField; }
            set { this.nameField = value; }
        }

        /// <remarks />
        public string Version {
            get { return this.versionField; }
            set { this.versionField = value; }
        }

        /// <remarks />
        public uint ProductType {
            get { return this.productTypeField; }
            set { this.productTypeField = value; }
        }

        /// <remarks />
        public AgentTopologyRequestOperatingSystemProcessorArchitecture ProcessorArchitecture {
            get { return this.processorArchitectureField; }
            set { this.processorArchitectureField = value; }
        }
    }

    /// <remarks />
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
    [Serializable()]
    [XmlType(AnonymousType = true, Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/")]
    public enum AgentTopologyRequestOperatingSystemProcessorArchitecture
    {
        /// <remarks />
        x64,

        /// <remarks />
        x86,
    }
}