// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using PipelineCost.Core;

namespace SentinelCost.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Kusto.Cloud.Platform.Data;
    using Kusto.Data;
    using Kusto.Ingest;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using WinLog;

    public class ConfigHelper
    {
        private string currentTrace2KustoConfigurationJson = String.Empty;

        public static IConfigurationRoot Configuration { get; set; }

        public bool IsKustoLoggingEnabled { get; private set; }

        public string ApplicationPath { get; set; }

        public TimeSpan FabricServiceActiveTimeSpan { get; private set; } = new TimeSpan(0, 0, 5);

        public TimeSpan SensorFabricHeartbeatStatusFreq { get; private set; } = new TimeSpan(0, 10, 0);

        public bool UploadEventFile { get; private set; }

        public bool ServiceIsActive { get; private set; }

        public DirectoryInfo SourceDirectoryInfo { get; private set; }

        public DirectoryInfo DestinationDirectoryInfo { get; private set; }

        public string SourceFileSearchPattern { get; private set; }

        public bool SearchSubDirectories { get; private set; }

        public bool RestartExtractTimerAfterEachRun { get; private set; }

        public bool UploadToAzureActive { get; private set; }

        public bool DownloadFromAzureActive { get; private set; }

        public Guid ApplicationKey { get; set; }

        public string ApplicationTitle { get; set; }

        public string ApplicationVersion { get; set; }

        public KeyVault KeyVault { get; set; }

        public KeyVaultInfo ServiceKeyVaultInfo { get; set; }

        public List<EventSinkInfo> ServiceEventSinkInfos { get; set; }

        public List<KustoIngestClient> KustoIngestClients { get; set; } = new List<KustoIngestClient>();

        public string ServiceName { get; set; }

        private readonly string[] eventLogFields;
        private readonly string[] sentinelCostMetricFields;

        public ConfigHelper()
        {
            var path = Assembly.GetExecutingAssembly().Location;
            ApplicationPath = Path.GetDirectoryName(path);
            InitializeLoggingConfiguration();

            eventLogFields = typeof(LogRecordCdoc).GetFields().Select(f => f.Name).ToArray();
            sentinelCostMetricFields = typeof(SentinelCostMetric).GetFields().Select(g => g.Name).ToArray();

            ServiceName = PipelineCostCommon.GetServiceName();
        }

        /// <summary>Intializes the logging configuration.</summary>
        /// <returns>A value indicating whether the Trace2Kusto configuration has changed</returns>
        public bool InitializeLoggingConfiguration()
        {
            // Determine the Environment JSON file to load, build, parse and instantiate
            DirectoryInfo dir_info = new DirectoryInfo(ApplicationPath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(dir_info.FullName)
                .AddJsonFile($"appsettings.json");

            Configuration = builder.Build();

            ServiceKeyVaultInfo = Configuration.GetSection("KeyVaultInfo").Get<KeyVaultInfo>();
            ServiceEventSinkInfos = Configuration.GetSection("EventSinkInfo").Get<List<EventSinkInfo>>();

            this.KeyVault = new KeyVault(ServiceKeyVaultInfo);

            // Initialize KustoClients
            foreach (EventSinkInfo serviceEventSinkInfo in ServiceEventSinkInfos)
            {
                var kcsb = new KustoConnectionStringBuilder(serviceEventSinkInfo.Settings.EndpointUrl)
                {
                    InitialCatalog = serviceEventSinkInfo.Settings.Database,
                    FederatedSecurity = true
                };

                string azureActiveDirectoryAppId = serviceEventSinkInfo.Settings.SinkId;
                string azureActiveDirectoryAppKey = KeyVault.GetSecret(serviceEventSinkInfo.Settings.SinkSecretKey);

                Console.WriteLine($"Create Kusto Client: {serviceEventSinkInfo.SinkAlias}");

                // Create the Event Log Ingest client
                var client = new KustoIngestClient
                {
                    IKustoIngestClient = GetKustoIngestClient(azureActiveDirectoryAppId, azureActiveDirectoryAppKey, serviceEventSinkInfo),
                    KustoIngestionProperties = new KustoIngestionProperties(kcsb.InitialCatalog, serviceEventSinkInfo.Settings.Table),
                    Name = serviceEventSinkInfo.SinkAlias
                };

                KustoIngestClients.Add(client);
            }

            return true;
        }

        public async void LoadDataToKusto(string sinkName, List<LogRecordSentinel> list)
        {
            try
            {
                var data = new EnumerableDataReader<LogRecordSentinel>(list, eventLogFields);
                var client = KustoIngestClients.FirstOrDefault(x => x.Name.Equals(sinkName));
                await client?.IKustoIngestClient.IngestFromDataReaderAsync(data, client.KustoIngestionProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadToLogFileKusto error: {ex}");
            }
        }

        public void LoadMetricsToKusto(string sinkName, List<SentinelCostMetric> list)
        {
            try
            {
                var data = new EnumerableDataReader<SentinelCostMetric>(list, sentinelCostMetricFields);
                var client = KustoIngestClients.FirstOrDefault(x => x.Name.Equals(sinkName));
                client?.IKustoIngestClient.IngestFromDataReader(data, client.KustoIngestionProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadToLogFileKusto error: {ex}");
            }
        }

        private IKustoIngestClient GetKustoIngestClient(string appId, string appKey, EventSinkInfo eventSinkInfo)
        {
            IKustoIngestClient _client;

            try
            {
                var kcsb = new KustoConnectionStringBuilder(eventSinkInfo.Settings.EndpointUrl)
                {
                    InitialCatalog = eventSinkInfo.Settings.Database,
                    FederatedSecurity = true
                };

                kcsb = kcsb.WithAadApplicationKeyAuthentication(appId, appKey, eventSinkInfo.Settings.Authority);
                _client = KustoIngestFactory.CreateQueuedIngestClient(kcsb);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return _client;
        }

        public Dictionary<string, object> GetFileInfoDictionary(FileInfo fileInfo)
        {
            return new Dictionary<string, object>()
            {
                { "Name", fileInfo.Name },
                { "DirectoryName", fileInfo.DirectoryName },
                { "Length", fileInfo.Length },
                { "CreationTimeUtc", fileInfo.CreationTimeUtc },
                { "LastAccessTimeUtc", fileInfo.LastAccessTimeUtc },
                { "LastWriteTimeUtc", fileInfo.LastWriteTimeUtc },
            };
        }

        public Dictionary<string, object> GetStopWatchDictionary(Stopwatch stopwatch)
        {
            var json = JsonConvert.SerializeObject(stopwatch, Formatting.Indented);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }
    }
}