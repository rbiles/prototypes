using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SIEMfx.SentinelWorkspacePoc
{
    public class SyslogToAzureBlobHelper
    {
        public CloudStorageAccount CloudStorageAccount { get; set; }

        public CloudBlobClient CloudBlobClient { get; set;}

        public CloudBlobContainer CloudBlobContainer { get; set;}
    }
}
