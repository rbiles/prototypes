using System;

namespace LogAnalyticsOdsApiHarness
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
    [Serializable()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/", IsNullable = false)]
    public class ConfigurationFileRequest
    {

        private byte[] publicKeyField;

        private uint requestedEncryptionAlgorithmField;

        private string managementGroupIdField;

        private string healthServiceIdField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "base64Binary")]
        public byte[] PublicKey
        {
            get
            {
                return this.publicKeyField;
            }
            set
            {
                this.publicKeyField = value;
            }
        }

        /// <remarks/>
        public uint RequestedEncryptionAlgorithm
        {
            get
            {
                return this.requestedEncryptionAlgorithmField;
            }
            set
            {
                this.requestedEncryptionAlgorithmField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string managementGroupId
        {
            get
            {
                return this.managementGroupIdField;
            }
            set
            {
                this.managementGroupIdField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string healthServiceId
        {
            get
            {
                return this.healthServiceIdField;
            }
            set
            {
                this.healthServiceIdField = value;
            }
        }
    }
}
