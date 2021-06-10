// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using GenevaETW.API.CustomTypes;
using Microsoft.Cloud.InstrumentationFramework;
using Microsoft.Cloud.InstrumentationFramework.Metrics.Extensions;

namespace GenevaETW.API
{
    /// <summary>
    ///     Metrics Manager, leveraged ARIS work, initially lifted from the ARIS repo
    /// </summary>
    public class SyntheticCounterManager
    {
        private readonly ulong allowableFileDelayInSeconds = 600;
        private readonly string LocationId;
        private readonly string MetricNamespace;

        private readonly IMdmMetric<DimensionValues11D, ulong> metricOneAgentEtwTcpNetworkBytes;
        private readonly IMdmMetric<DimensionValues11D, ulong> metricOneAgentEtwTcpNetworkCount;

        private readonly string MonitoringAccount;

        public SyntheticCounterManager(GenevaMdmConfiguration cfg)
        {
            MetricNamespace = cfg.MetricsNamespace;
            MonitoringAccount =
                cfg.MetricsAccount; // not sure if it needs to the Logs Account value since currently in PPE they are the same 

            // Get the location information for this "unit of deployment" - region in Azure
            LocationId = cfg.LocationId;

            // Start in-memory aggregation and publication of metrics (such as histogram calculation)
            if (!MdmMetricController.StartMetricPublication())
                SIEMfxEventSource.Log.Information("IfxMetrics", "Ifx Configuration - Error - cannot publish metrics");

            // Use the factory helper class to generate the synthetic metrics
            var metricFactory = new MdmMetricFactory();

            // Define the histogram bucketing configuration
            var latencyBehavior = new MdmBucketedDistributionBehavior
            {
                MinimumValue = cfg.MinimumValue,
                BucketSize = cfg.BucketSize,
                BucketCount = cfg.BucketCount
            };


            metricOneAgentEtwTcpNetworkBytes = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocOneAgentEtwTcpNetworkBytes",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "TimeCreated",
                "EventId",
                "ProcessName",
                "ProcessId",
                "DestinationIpAddress",
                "DestinationPort",
                "SourceIpAddress",
                "SourcePort",
                "Bytes"
            );

            metricOneAgentEtwTcpNetworkCount = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocOneAgentEtwTcpNetworkCount",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "TimeCreated",
                "EventId",
                "ProcessName",
                "ProcessId",
                "DestinationIpAddress",
                "DestinationPort",
                "SourceIpAddress",
                "SourcePort",
                "Count"
            );

            SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration Initialized -
                MetricNamespace: {MetricNamespace}, MonitoringAccount: {MonitoringAccount}");
        }

        public void InsertEtwEventTcpNetwork(string customerResourceId, IDictionary<string, object> eventData)
        {
            var dimBytesValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                eventData["TimeCreated"].ToString(),
                eventData["EventId"].ToString(),
                eventData["ProcessName"].ToString(),
                eventData["ProcessId"].ToString(),
                eventData["DestinationIpAddress"].ToString(),
                eventData["DestinationPort"].ToString(),
                eventData["SourceIpAddress"].ToString(),
                eventData["SourcePort"].ToString(),
                eventData["Bytes"].ToString()
            );

            var dimCountValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                eventData["TimeCreated"].ToString(),
                eventData["EventId"].ToString(),
                eventData["ProcessName"].ToString(),
                eventData["ProcessId"].ToString(),
                eventData["DestinationIpAddress"].ToString(),
                eventData["DestinationPort"].ToString(),
                eventData["SourceIpAddress"].ToString(),
                eventData["SourcePort"].ToString(),
                eventData["Count"].ToString()
            );

            // Updates the latency histogram
            var successCount = metricOneAgentEtwTcpNetworkCount?.Set(
                value: Convert.ToUInt64(eventData["Count"]), dimCountValues) ?? false;

            // Updates the latency histogram
            var successBytes = metricOneAgentEtwTcpNetworkBytes?.Set(
                Convert.ToUInt64(eventData["Bytes"]), dimBytesValues) ?? false;

            if (successBytes || successCount)
                SIEMfxEventSource.Log.Information("EtwEvent",
                    $@"Ifx update File Success Rate histogram {eventData}, {"Values"}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update file latency measure for {customerResourceId}");
        }
    }
}