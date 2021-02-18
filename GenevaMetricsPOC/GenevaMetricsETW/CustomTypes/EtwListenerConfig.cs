using System;

namespace GenevaEtwPOC.CustomTypes
{
    public class EtwListenerConfig
    {
        public string SessionName { get; set; } 

        public string ProviderName { get; set; } 

        public Guid ProviderId { get; set; } 

        public string ObservableName { get; set; }

        public string KqlQuery { get; set; } 
    }
}
