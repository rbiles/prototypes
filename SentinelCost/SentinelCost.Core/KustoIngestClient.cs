using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelCost.Core
{
    using Kusto.Ingest;

    public class KustoIngestClient
    {
        public string Name { get; set; }
        
        public IKustoIngestClient IKustoIngestClient { get; set; }

        public KustoIngestionProperties KustoIngestionProperties { get; set; }
    }
}
