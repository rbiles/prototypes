// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using SIEMfx.SentinelWorkspacePoc.CustomTypes;

namespace SIEMfx.SentinelWorkspacePoc
{
    public class SyslogToAzureBlob
    {
        public SyslogToAzureBlob(SentinelApiConfig sentinelApiConfig, string azureStorageConnectionString)
        {
            SentinelApiConfig = sentinelApiConfig;
            AzureStorageConnectionString = azureStorageConnectionString;

            SyslogToAzureBlobHelpers = new Dictionary<string, SyslogToAzureBlobHelper>();
        }

        private Dictionary<string, SyslogToAzureBlobHelper> SyslogToAzureBlobHelpers { get; set; }

        private string AzureStorageConnectionString { get; }

        public SentinelApiConfig SentinelApiConfig { get; }

        public async Task UploadFileToBlobStorageAsync(string uploadJson,
             string sentinelDataType)
        {
            try
            {
                var syslogToAzureBlobHelper = GetSyslogToAzureBlobHelper(sentinelDataType.ToLower());

                if (syslogToAzureBlobHelper != null)
                {
                    // Upload only if the json content of the Sentinel batch
                    var blobFilepath = $"{sentinelDataType.ToLower()}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.json";
                    var folderName = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    // Get a reference to the blob address, then upload the file to the blob.
                    var cloudBlockBlob =
                        syslogToAzureBlobHelper.CloudBlobContainer
                            .GetBlockBlobReference($"{folderName}/{blobFilepath}");

                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(uploadJson)))
                    {
                        await cloudBlockBlob.UploadFromStreamAsync(ms);
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"WARNING: Error retrieving blob container for data type: {sentinelDataType.ToLower()}!!!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private SyslogToAzureBlobHelper GetSyslogToAzureBlobHelper(string dataTypeName)
        {
            try
            {
                if (!SyslogToAzureBlobHelpers.ContainsKey(dataTypeName))
                {
                    CloudStorageAccount.TryParse(AzureStorageConnectionString,
                        out var storageAccount);

                    // Create the CloudBlobClient that represents the 
                    // Blob storage endpoint for the storage account.
                    var cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Create the container if it doesnt exist
                    var cloudBlobContainer = cloudBlobClient.GetContainerReference(dataTypeName);
                    var success = cloudBlobContainer.CreateIfNotExists();

                    SyslogToAzureBlobHelpers.Add(dataTypeName, new SyslogToAzureBlobHelper
                    {
                        CloudBlobClient = cloudBlobClient,
                        CloudBlobContainer = cloudBlobContainer,
                        CloudStorageAccount = storageAccount
                    });
                }

                return SyslogToAzureBlobHelpers[dataTypeName];
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
    }
}