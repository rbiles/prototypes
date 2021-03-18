// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Cloud.InstrumentationFramework;
using Microsoft.Cloud.InstrumentationFramework.Metrics.Extensions;

namespace GenevaETW.API
{
    /// <summary>
    ///     Metrics Manager, leveraged ARIS work, initially lifted from the ARIS repo
    /// </summary>
    public class MetricsManager
    {
        private readonly ulong allowableFileDelayInSeconds = 600;
        private readonly string LocationId;
        private readonly IMdmMetric<DimensionValues3D, ulong> metricMeasureForFileDataLatency;
        private readonly IMdmMetric<DimensionValues3D, ulong> metricMeasureForFileSuccessRate;
        private readonly IMdmMetric<DimensionValues3D, ulong> metricMeasureForServerCpu;
        private readonly IMdmMetric<DimensionValues3D, ulong> metricMeasureForServiceUp;

        private readonly string MetricNamespace;
        private readonly IMdmMetric<DimensionValues4D, ulong> metricOneAgentEtwEvent;

        private readonly MeasureMetric7D metricOneAgentEtwDestinationBytes;
        private readonly IMdmMetric<DimensionValues11D, ulong> metricOneAgentEtwTcpNetworkBytes;
        private readonly IMdmMetric<DimensionValues11D, ulong> metricOneAgentEtwTcpNetworkCount;

        private readonly string MonitoringAccount;

        public MetricsManager(SloMetricsConfiguration cfg)
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

            // Define a unified measure for service heartbeat
            metricMeasureForServiceUp = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocWecServiceUp",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "Status"
            );

            // Define a unified measure for service heartbeat
            metricMeasureForServerCpu = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocWecServiceCpu",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "AvgCpu"
            );

            // Define a unified measure for service heartbeat
            metricMeasureForFileDataLatency = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocWecFileLatency",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "FileDelaySeconds"
            );

            // Define a unified measure for service heartbeat
            metricMeasureForFileSuccessRate = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocWecServiceSuccessRate",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "SuccessMetric"
            );

            // Define a unified measure for service heartbeat
            metricOneAgentEtwEvent = metricFactory.CreateUInt64Metric(
                MdmMetricFlags.CumulativeMetricDefault,
                MonitoringAccount,
                MetricNamespace,
                "CdocOneAgentEtwEvent",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "RecordType",
                "LastRecordWritten"
            );

            metricOneAgentEtwDestinationBytes = MeasureMetric7D.Create(
                MonitoringAccount,
                MetricNamespace,
                "CdocOneAgentEtwDestinationBytes",
                "CustomerResourceId", // Mandatory customer resource dimension
                "LocationId", // Mandatory topology dimension
                "TimeCreated",
                "DestinationIpAddress",
                "ProcessId",
                "ProcessName",
                "Bytes"
            );

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

            if (metricMeasureForServiceUp == null)
            {
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not create latency measure for MetricNamespace: {MetricNamespace}, MonitoringAccount: {MonitoringAccount}");
                return;
            }

            SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration Initialized -
                MetricNamespace: {MetricNamespace}, MonitoringAccount: {MonitoringAccount}");
        }

        public void MeasureServiceUp(ulong elapsedMs, string customerResourceId)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                true.ToString()
            );

            // Updates the latency histogram
            var success = metricMeasureForServiceUp?.Set(elapsedMs, dimValues) ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("IfxMetrics",
                    $@"Ifx update latency histogram {elapsedMs}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update latency measure for {customerResourceId}");
        }

        public void MeasureCpuUtil(ulong cpuUtil, string customerResourceId)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                cpuUtil.ToString()
            );

            // Updates the latency histogram
            var success = metricMeasureForServerCpu?.Set(cpuUtil, dimValues) ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx update CPU histogram {cpuUtil}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update cpu measure for {customerResourceId}");
        }

        public void MeasureFileDelay(ulong fileDelayInSeconds, string customerResourceId)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                fileDelayInSeconds.ToString()
            );

            // Updates the latency histogram
            var success = metricMeasureForFileDataLatency?.Set(fileDelayInSeconds, dimValues) ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("IfxMetrics",
                    $@"Ifx update File Latency histogram {fileDelayInSeconds}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update file latency measure for {customerResourceId}");
        }

        public void MeasureSuccessRate(ulong fileDelayInSeconds, string customerResourceId)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                fileDelayInSeconds > allowableFileDelayInSeconds ? "Failed" : "Success"
            );

            // Updates the latency histogram
            var success = metricMeasureForFileSuccessRate?.Set(fileDelayInSeconds, dimValues) ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("IfxMetrics",
                    $@"Ifx update File Success Rate histogram {fileDelayInSeconds}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update file latency measure for {customerResourceId}");
        }


        public void InsertEtwEventToGeneva(ulong lastRecordWrittenUlong, string customerResourceId, string eventData)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                "CdocEtwEvent",
                lastRecordWrittenUlong.ToString()
            );

            // Updates the latency histogram
            var success = metricOneAgentEtwEvent?.Set(lastRecordWrittenUlong, dimValues) ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("EtwEvent",
                    $@"Ifx update File Success Rate histogram {eventData}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update file latency measure for {customerResourceId}");
        }

        public void InsertEtwEventDestinationBytes(string customerResourceId, IDictionary<string, object> eventData)
        {
            var dimValues = DimensionValues.Create(
                customerResourceId,
                Environment.MachineName,
                "TimeCreated",
                "DestinationIpAddress",
                "ProcessId",
                "ProcessName",
                "Bytes"
            );

            // Updates the latency histogram
            var success = metricOneAgentEtwDestinationBytes?.LogValue(
                (long)eventData["Bytes"],
                customerResourceId,
                Environment.MachineName,
                eventData["TimeCreated"].ToString(),
                eventData["DestinationIpAddress"].ToString(),
                eventData["ProcessId"].ToString(),
                eventData["ProcessName"].ToString(),
                "Bytes") ?? false;

            if (success)
                SIEMfxEventSource.Log.Information("EtwEvent",
                    $@"Ifx update File Success Rate histogram {eventData}, {dimValues}");
            else
                SIEMfxEventSource.Log.Information("IfxMetrics", $@"Ifx Configuration - 
                    Could not update file latency measure for {customerResourceId}");
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