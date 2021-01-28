// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace LogAnalyticsOdsApiHarness.CustomTypes
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Eventing.Reader;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using Event.Ingest;
    using Event.Ingest.Components;
    using Event.Ingest.Diagnostics;
    using Event.Ingest.Ods;
    using Microsoft.Win32;

    public class WindowsEventPayload
    {
        private readonly string headerTemplate =
            "<DataItems IPName=\"{0}\" ManagementGroupId=\"{1}\" HealthServiceSourceId=\"{2}\" DataType=\"{3}\">";

        private readonly string footerTemplate = "</DataItems>";

        private OdsUploaderConfig config { get; set; }

        public int BatchItemCount = 0;

        public TimeSpan BatchTimeSpan;

        public Uploader<string> Uploader { get; set; }

        public string ManagementGroupId { get; set; }

        public string DataType { get; set; }

        public string IpName { get; set; }

        public ConcurrentBag<string> DataItems { get; set; }

        public string WorkspaceId { get; set; }

        public SentinelApiConfig SentinenApiConfig { get; set; }

        public string DataItemTemplate { get; set; } =
            "<DataItem type=\"System.Event.LinkedData\" time=\"{EventTimeUTC}\" sourceHealthServiceId=\"{WorkspaceId}\"><EventOriginId>{7C384BE3-8EBD-4B86-A392-357AA34750C5}</EventOriginId><PublisherId>{{ProviderGuid}}</PublisherId><PublisherName>{Provider}</PublisherName><EventSourceName>{EventSource}</EventSourceName><Channel>{Channel}</Channel><LoggingComputer>{Computer}</LoggingComputer><EventNumber>{EventId}</EventNumber><EventCategory>{EventCategory}</EventCategory><EventLevel>{EventLevel}</EventLevel><UserName>N/A</UserName><RawDescription></RawDescription><LCID>1033</LCID><CollectDescription>True</CollectDescription><EventData><DataItem type=\"System.XmlData\" time=\"{EventTimeUTC}\" sourceHealthServiceId=\"{WorkspaceId}\">{EventData}</DataItem></EventData><EventDisplayNumber>{EventId}</EventDisplayNumber><EventDescription></EventDescription><ManagedEntityId>{D056ADDA-9675-7690-CC92-41AA6B90CC05}</ManagedEntityId><RuleId>{1F68E37D-EC73-9BD3-92D5-C236C995FA0A}</RuleId></DataItem>\r\n";

        public WindowsEventPayload()
        {
            DataItems = new ConcurrentBag<string>();
        }

        public void InitializeEventIngest()
        {
            config = new OdsUploaderConfig()
            {
                BatchSize = SentinenApiConfig.EventIngestBatchSize,
                MaxItemLingerTime = TimeSpan.FromMilliseconds(SentinenApiConfig.MaxItemLingerTime),
                WorkspaceId = WorkspaceId,
                MaxIngestorCount = SentinenApiConfig.MaxIngestorCount,
            };

            Uploader = OdsUploadHelper.CreateUploader(config);
            Uploader.BatchAction += Uploader_BatchAction;
            //UploaderEventSource.Instance.GetDefaultListener().LogRecordReceived += Uploader_LogRecordReceived;
        }

        public string GetLogAnalyticsResourceId(string workspaceId)
        {
            try
            {
                // Path to the GUID for the current and valid certificate on the LA Workspace
                string resourceIdRegKeyPath =
                    $@"SYSTEM\CurrentControlSet\Services\HealthService\Parameters";

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(resourceIdRegKeyPath))
                {
                    Object regKeyValue = key.GetValue("Azure Resource Id");
                    if (regKeyValue != null)
                    {
                        //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
                        return regKeyValue as string;
                    }
                }
            }
            catch (Exception)
            {
                // Nothing
            }
            
            // This interpolated string, although it looks hacked together, is correct and validation on the ODS Web service, IF it is provided as a request header,
            // requires the below structure with the required fields (subscriptions, resourceGroups, and providers, along with the correct amount of forward slashes (trial and error)
            string nonAzureResourceId = $"/subscriptions/{workspaceId}/resourceGroups/none/providers/computer/physical/{GetFQDN()}";
            return nonAzureResourceId;
        }

        private string GetFQDN()
        {
            //TODO: This should be moved somewhere common across SIEMfx common libraries
            string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName))
            {
                hostName += domainName;   
            }

            return hostName;                   
        }

        private void Uploader_BatchAction(object sender, BatchActionEventArgs<string> e)
        {
            if (e.Batch.Action == UploaderAction.Ingest)
            {
                BatchItemCount += e.Batch.ItemCount;
                BatchTimeSpan += e.Batch.Duration;
            }
        }

        private static void Uploader_LogRecordReceived(object sender, Event.Ingest.Diagnostics.UploaderLogRecordEventArgs e)
        {
            Console.WriteLine(e.Record.Message);
        }

        public void AddEvent(EtwListener etwListener, IDictionary<string, object> evt, bool useEventIngest)
        {
            var returnXmlWriterValue = XmlWriterEtwEventDictionary(etwListener, evt);
            AddToPayload(returnXmlWriterValue, useEventIngest);
        }

        public void AddEvent(EventRecord eventRecord, bool useEventIngest, XmlCreationMechanism xmlCreationMechanism)
        {
            switch (xmlCreationMechanism)
            {
                case XmlCreationMechanism.StringReplacement:
                    DateTime timeCreated = (DateTime) eventRecord.TimeCreated;
                    string tempWinEvent = DataItemTemplate;
                    tempWinEvent = tempWinEvent.Replace("{WorkspaceId}", WorkspaceId);
                    tempWinEvent = tempWinEvent.Replace("{ProviderGuid}", (eventRecord.ProviderId ?? Guid.Empty).ToString());
                    tempWinEvent = tempWinEvent.Replace("{Provider}", eventRecord.ProviderName);
                    tempWinEvent = tempWinEvent.Replace("{EventSource}", eventRecord.ProviderName);
                    tempWinEvent = tempWinEvent.Replace("{Channel}", eventRecord.LogName ?? "Unknown");
                    tempWinEvent = tempWinEvent.Replace("{Computer}", eventRecord.MachineName);
                    tempWinEvent = tempWinEvent.Replace("{EventId}", eventRecord.Id.ToString());
                    tempWinEvent = tempWinEvent.Replace("{EventCategory}", (eventRecord.Task ?? 0).ToString());
                    tempWinEvent = tempWinEvent.Replace("{EventLevel}", (eventRecord.Level ?? 0).ToString());
                    tempWinEvent = tempWinEvent.Replace("{EventTimeUTC}", $"{timeCreated.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.ffffffZ}");
                    tempWinEvent = tempWinEvent.Replace("{EventData}", WinLog.LogReader.RetrieveExtendedData(eventRecord.ToXml()));
                    AddToPayload(tempWinEvent, useEventIngest);
                    break;
                case XmlCreationMechanism.XElement:
                    //var taskReturnValue = TestWriter(eventRecord);
                    var returnValue = LinqXElementWriter(eventRecord);
                    returnValue = returnValue.Replace("&lt;", "<").Replace("&gt;", ">");
                    AddToPayload(returnValue, useEventIngest);
                    break;
                case XmlCreationMechanism.XmlWriter:
                    var returnXmlWriterValue = XmlWriterEventRecord(eventRecord);
                    AddToPayload(returnXmlWriterValue, useEventIngest);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(xmlCreationMechanism), xmlCreationMechanism, null);
            }
        }

        private string LinqXElementWriter(EventRecord eventRecord)
        {
            DateTime timeCreated = (DateTime) eventRecord.TimeCreated;
            string eventData = WinLog.LogReader.RetrieveExtendedData(eventRecord.ToXml());

            XElement dataItemElement =
                new XElement("DataItem",
                    new XAttribute("type", "System.Event.LinkedData"),
                    new XAttribute("time", $"{timeCreated.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.ffffffZ}"),
                    new XAttribute("sourceHealthServiceId", $"{WorkspaceId}"),
                    new XElement("EventOriginId", "{7C384BE3-8EBD-4B86-A392-357AA34750C5}"),
                    new XElement("PublisherId", $"{{{(eventRecord.ProviderId ?? Guid.Empty).ToString()}}}"),
                    new XElement("PublisherName", eventRecord.ProviderName),
                    new XElement("EventSourceName", eventRecord.ProviderName),
                    new XElement("Channel", $"{eventRecord.LogName ?? "Unknown"}"),
                    new XElement("LoggingComputer", eventRecord.MachineName),
                    new XElement("EventNumber", $"{eventRecord.Id.ToString()}"),
                    new XElement("EventCategory", $"{(eventRecord.Task ?? 0).ToString()}"),
                    new XElement("EventLevel", $"{(eventRecord.Level ?? 0).ToString()}"),
                    new XElement("UserName", "N/A"),
                    new XElement("RawDescription", string.Empty),
                    new XElement("LCID", "1033"),
                    new XElement("CollectDescription", "true"),
                    new XElement("EventData",
                        new XElement("DataItem",
                            new XAttribute("type", "System.XmlData"),
                            new XAttribute("time", $"{timeCreated.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.ffffffZ}"),
                            new XAttribute("sourceHealthServiceId", $"{WorkspaceId}"),
                            eventData)),
                    new XElement("EventDisplayNumber", $"{eventRecord.Id.ToString()}"),
                    new XElement("EventDescription", string.Empty),
                    new XElement("ManagedEntityId", "{D056ADDA-9675-7690-CC92-41AA6B90CC05}"),
                    new XElement("RuleId", "{1F68E37D-EC73-9BD3-92D5-C236C995FA0A}")
                );

            return dataItemElement.ToString(SaveOptions.DisableFormatting);
        }

        private string XmlWriterEventRecord(EventRecord eventRecord)
        {
            var sb = new StringBuilder();
            var stt = new XmlWriterSettings();
            stt.ConformanceLevel = ConformanceLevel.Fragment;
            using (var writer = System.Xml.XmlWriter.Create(sb, stt))
            {
                // See below in comments the OLD template for data item XML
                var eventTimeUtc = eventRecord.TimeCreated?.ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
                // DataItem header
                writer.WriteStartElement("DataItem");
                writer.WriteAttributeString("type", "System.Event.LinkedData");
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", WorkspaceId);
                //Nested elements
                writer.WriteElementString("EventOriginId", "{7C384BE3-8EBD-4B86-A392-357AA34750C5}");
                writer.WriteElementString("PublisherId", $"{{{(eventRecord.ProviderId ?? Guid.Empty).ToString()}}}");
                writer.WriteElementString("PublisherName", eventRecord.ProviderName);
                writer.WriteElementString("EventSourceName", eventRecord.ProviderName);
                writer.WriteElementString("Channel", eventRecord.LogName ?? "Unknown");
                writer.WriteElementString("LoggingComputer", eventRecord.MachineName);
                writer.WriteElementString("EventNumber", eventRecord.Id.ToString());
                writer.WriteElementString("EventCategory", (eventRecord.Task ?? 0).ToString());
                writer.WriteElementString("EventLevel", (eventRecord.Level ?? 0).ToString());
                writer.WriteElementString("UserName", "N/A");
                writer.WriteElementString("RawDescription", string.Empty);
                writer.WriteElementString("LCID", "1033");
                writer.WriteElementString("CollectDescription", "True");
                // EventData with nested data item
                writer.WriteStartElement("EventData");
                writer.WriteStartElement("DataItem");
                writer.WriteAttributeString("type", "System.XmlData");
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", WorkspaceId);
                var xmlEventData = WinLog.LogReader.RetrieveExtendedData(eventRecord.ToXml());
                writer.WriteRaw(xmlEventData); //write DataItem content
                writer.WriteFullEndElement(); // close DataItem
                writer.WriteFullEndElement(); // close EventData
                // end EventData

                writer.WriteElementString("EventDisplayNumber", eventRecord.Id.ToString());
                writer.WriteElementString("EventDescription", string.Empty);
                writer.WriteElementString("ManagedEntityId", "{D056ADDA-9675-7690-CC92-41AA6B90CC05}");
                writer.WriteElementString("RuleId", "{1F68E37D-EC73-9BD3-92D5-C236C995FA0A}");
                writer.WriteFullEndElement(); // </DataItem>
                writer.Flush();
            }

            var xml = sb.ToString();
            return xml;
        }

        private string XmlWriterEtwEventDictionary(EtwListener etwListener, IDictionary<string, object> eventValue)
        {
            var sb = new StringBuilder();
            var stt = new XmlWriterSettings();
            stt.ConformanceLevel = ConformanceLevel.Fragment;
            using (var writer = System.Xml.XmlWriter.Create(sb, stt))
            {
                DateTime? summaryDateTime = Convert.ToDateTime(eventValue["TimeCreated"]);
                var eventTimeUtc = summaryDateTime?.ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

                // DataItem header
                writer.WriteStartElement("DataItem");
                writer.WriteAttributeString("type", "System.Event.LinkedData");
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", WorkspaceId);

                //Nested elements
                writer.WriteElementString("EventOriginId", "{7C384BE3-8EBD-4B86-A392-357AA34750C5}");
                writer.WriteElementString("PublisherId", $"{{{etwListener.EtwListenerConfig.ProviderId.ToString()}}}");
                writer.WriteElementString("PublisherName", etwListener.EtwListenerConfig.ProviderName);
                writer.WriteElementString("EventSourceName", etwListener.EtwListenerConfig.ProviderName);
                writer.WriteElementString("Channel", etwListener.EtwListenerConfig.ObservableName);
                writer.WriteElementString("LoggingComputer", Global.GetMachineFqdn());
                writer.WriteElementString("EventNumber", eventValue["EventId"].ToString());
                writer.WriteElementString("EventCategory", "0");
                writer.WriteElementString("EventLevel", "0");
                writer.WriteElementString("UserName", "N/A");
                writer.WriteElementString("RawDescription", string.Empty);
                writer.WriteElementString("LCID", "1033");
                writer.WriteElementString("CollectDescription", "True");

                // EventData with nested data item
                // Create the EventData from the dictionary object
                // go through the items in the dictionary and copy over the key value pairs)
                writer.WriteStartElement("EventData");
                writer.WriteStartElement("DataItem");
                writer.WriteAttributeString("type", "System.XmlData");
                writer.WriteAttributeString("time", eventTimeUtc);
                writer.WriteAttributeString("sourceHealthServiceId", WorkspaceId);
                var xmlEventData = XmlWriterEtwEventDataDictionary(etwListener, eventValue);
                writer.WriteRaw(xmlEventData); //write DataItem content
                writer.WriteFullEndElement(); // close DataItem
                writer.WriteFullEndElement(); // close EventData

                writer.WriteElementString("EventDisplayNumber", eventValue["EventId"].ToString());
                writer.WriteElementString("EventDescription", string.Empty);
                writer.WriteElementString("ManagedEntityId", "{D056ADDA-9675-7690-CC92-41AA6B90CC05}");
                writer.WriteElementString("RuleId", "{1F68E37D-EC73-9BD3-92D5-C236C995FA0A}");
                writer.WriteFullEndElement(); // </DataItem>
                writer.Flush();
            }

            var xml = sb.ToString();
            return xml;
        }

        private string XmlWriterEtwEventDataDictionary(EtwListener etwListener, IDictionary<string, object> eventValue)
        {
            var sb = new StringBuilder();
            var stt = new XmlWriterSettings();
            stt.ConformanceLevel = ConformanceLevel.Fragment;
            using (var writer = System.Xml.XmlWriter.Create(sb, stt))
            {
                DateTime? summaryDateTime = Convert.ToDateTime(eventValue["TimeCreated"]);
                var eventTimeUtc = summaryDateTime?.ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

                writer.WriteStartElement("EventData", "http://schemas.microsoft.com/win/2004/08/events/event");
                foreach (var kvp in eventValue)
                {
                    writer.WriteStartElement("Data");
                    writer.WriteAttributeString("Name", kvp.Key);
                    writer.WriteRaw(kvp.Value.ToString()); //write Data content
                    writer.WriteFullEndElement(); // close Data
                }

                writer.WriteFullEndElement(); // close EventData
                writer.Flush();
            }

            var xml = sb.ToString();
            return xml;
        }

        public void AddToPayload(string dataItem, bool useEventIngest)
        {
            if (useEventIngest)
            {
                Uploader.OnNext(dataItem);
            }
            else
            {
                DataItems.Add(dataItem);
            }
        }

        public string GetUploadBatch(IEnumerable<string> dataItems)
        {
            // Build object and header
            StringBuilder uploadBatchBuilder = new StringBuilder();
            uploadBatchBuilder.AppendLine(string.Format(headerTemplate, IpName, ManagementGroupId, WorkspaceId, DataType));

            // Add data items
            foreach (string dataItem in dataItems)
            {
                uploadBatchBuilder.AppendLine($"\t{dataItem}");
            }

            // Add the footer
            uploadBatchBuilder.AppendLine(footerTemplate);

            return uploadBatchBuilder.ToString();
        }

        /// <summary>
        ///     Break a list of items into chunks of a specific size
        /// </summary>
        public IEnumerable<IEnumerable<T>> SplitListIntoChunks<T>(int parts)
        {
            int i = 0;
            var splits = from item in DataItems
                group item by i++ % parts
                into part
                select part.AsEnumerable();

            return (IEnumerable<IEnumerable<T>>) splits;
        }

        public override string ToString()
        {
            // Build object and header
            StringBuilder returnString = new StringBuilder();
            returnString.AppendLine(string.Format(headerTemplate, IpName, ManagementGroupId, WorkspaceId, DataType));

            // Add data items
            foreach (string dataItem in DataItems)
            {
                returnString.AppendLine($"\t{dataItem}");
            }

            // Add the footer
            returnString.AppendLine();

            return returnString.ToString();
        }
    }
}