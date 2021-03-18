// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Eventing.Reader;
    using System.Linq;
    using System.Security.Policy;
    using System.Xml.Linq;
    using Newtonsoft.Json;
    using WinLog;
    using WinLog.Helpers;

    public enum EventType
    {
        Send = 0,
        Receive
    }

    public enum DataType
    {
        Xml = 0,
        Dictionary
    }

    public enum CompressionType
    {
        Clear = 0,
        Compressed
    }

    public enum EventLogName
    {
        Application = 0,
        Security,
        System,
        ForwardedEvents
    }
    /// <summary>
    ///     Class that defines the event metadata.
    /// </summary>
    public class EventIdMetrics
    {
        public int EventId { get; set; }

        public string EventProvider { get; set; }

        public int EventCount { get; set; }

        public string EventXml { get; set; }

        public string EventChannel { get; set; }
    }

    /// <summary>
    ///     Class that defines the various metrics associated with event uploading.
    /// </summary>
    public class EventLogUploadResult
    {
        /// <summary>
        ///     The number of event records that were uploaded.
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        ///     The number of events that matched the upload filter criteria.
        /// </summary>
        public int FilteredEventCount { get; set; }

        /// <summary>
        ///     The number of seconds that it took the program to read all of the events from the event source.
        /// </summary>
        public double TimeToRead { get; set; }

        /// <summary>
        ///     The number of seconds that it took to complete uploading the events to the destination.
        /// </summary>
        public double TimeToUpload { get; set; }

        /// <summary>
        ///     Whether the upload completed successfully.
        /// </summary>
        public bool UploadSuccessful { get; set; }

        /// <summary>
        ///     The returned error message, if any.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     The list of EventIdMetrics uploaded.
        /// </summary>
        public Dictionary<string, EventIdMetrics> EventIdMetricsList { get; set; }

        /// <summary>
        ///     The number of events without User Data.
        /// </summary>
        public int NullUserDataCount { get; set; }

        /// <summary>
        ///     The number of events without extended Event Data.
        /// </summary>
        public int NullEventDataCount { get; set; }
    }

    /// <summary>
    ///     Class that defines an event log record for the purposes of the WinLog namespace.
    /// </summary>
    public class LogRecord
    {
        public string Provider;
        public int EventId;
        public string Version;
        public string Level;
        public string Task;
        public string Opcode;
        public string Keywords;
        public DateTime TimeCreated;
        public long EventRecordId;
        public Guid Correlation; //missing in LogRecordCdoc
        public int ProcessId;
        public int ThreadId;
        public string Channel;
        public string Computer;
        public string Security;
        public dynamic EventData;
        public dynamic LogFileLineage;

        public EventBookmark Bookmark { get; private set; }

        public LogRecord(EventBookmark bookmark)
        {
            Bookmark = bookmark;
        }

        public LogRecord()
        {
        }

        public LogRecord(dynamic record, EventBookmark bookmark)
        {
            Bookmark = bookmark;

            SetCommonAttributes(record);
        }

        internal LogRecord(dynamic record)
        {
            SetCommonAttributes(record);
        }

        private void SetCommonAttributes(dynamic record)
        {
            IDictionary<string, object> dictionaryRecord = record;

            Provider = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Provider");
            EventId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "EventId"));
            TimeCreated = Convert.ToDateTime(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "TimeCreated"));
            Computer = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Computer");
            EventRecordId = Convert.ToInt64(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "EventRecordId"));

            if (dictionaryRecord.ContainsKey("EventData"))
            {
                EventData = JsonConvert.SerializeObject(dictionaryRecord["EventData"], Formatting.Indented);
            }

            // Newly added properties
            Version = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Version");
            Level = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Level");
            Task = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Task");
            Opcode = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Opcode");
            Security = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Security");
            Channel = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Channel");

            Keywords = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Keywords");

            Guid resultCorrelation;
            if (Guid.TryParse(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Correlation"), out resultCorrelation))
            {
                Correlation = resultCorrelation;
            }

            // Variant System properties (not on all Windows Events)
            string processId = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ProcessID");
            if (!string.IsNullOrEmpty(processId))
            {
                ProcessId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ProcessID"));
            }

            string threadId = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ThreadID");
            if (!string.IsNullOrEmpty(threadId))
            {
                ThreadId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ThreadID"));
            }
        }
    }

    public class SentinelCostMetric
    {
        public long PackageId;
        public Guid PackageGuid;
        public DateTime OccurenceUtc;
        public string MachineName;
        public string ServiceName;
        public string EventType;
        public dynamic MetricData;

        public SentinelCostMetric()
        {
        }
    }

    /// <summary>
    ///     Class that defines an event log record for the purposes of the Microsoft Cyber Defense Operations Center (CDOC).
    /// </summary>
    public class LogRecordSentinel
    {
        public string Provider;
        public int EventId;
        public string Version;
        public string Level;
        public string Task;
        public string Opcode;
        public DateTime TimeCreated;
        public long EventRecordId;
        public int ProcessId;
        public int ThreadId;
        public string Channel;
        public string Computer;
        public string Security;
        public dynamic EventData;
        public Dictionary<string, object> LogFileLineage;

        public EventBookmark Bookmark { get; private set; }

        public LogRecordSentinel(EventBookmark bookmark)
        {
            Bookmark = bookmark;
        }

        public LogRecordSentinel()
        {
        }

        public LogRecordSentinel(dynamic record, EventBookmark bookmark, string serviceName)
        {
            Bookmark = bookmark;

            SetCommonAttributes(record, serviceName);
        }

        internal LogRecordSentinel(dynamic record, string serviceName)
        {
            SetCommonAttributes(record, serviceName);
        }

        private void SetCommonAttributes(dynamic record, string serviceName)
        {
            IDictionary<string, object> dictionaryRecord = record;

            Provider = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Provider");
            EventId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "EventId"));
            TimeCreated = Convert.ToDateTime(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "TimeCreated"));
            Computer = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Computer");
            EventRecordId = Convert.ToInt64(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "EventRecordId"));

            EventData = dictionaryRecord["EventData"];

            // Newly added properties
            Version = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Version");
            Level = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Level");
            Task = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Task");
            Opcode = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Opcode");
            Security = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Security");
            Channel = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "Channel");

            // Variant System properties (not on all Windows Events)
            string processId = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ProcessID");
            if (!string.IsNullOrEmpty(processId))
            {
                ProcessId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ProcessID"));
            }

            string threadId = CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ThreadID");
            if (!string.IsNullOrEmpty(threadId))
            {
                ThreadId = Convert.ToInt32(CommonXmlFunctions.GetSafeExpandoObjectValue(dictionaryRecord, "ThreadID"));
            }

            // Set LogFileLineage values
            var collectorTimestamp = DateTime.UtcNow;
            var logFileLineage = new Dictionary<string, object>
            {
                { "UploadMachine", Environment.MachineName },
                { "CollectorTimeStamp", collectorTimestamp },
                { "CollectorUnixTimeStamp", collectorTimestamp.GetUnixTime() },
                { "ServiceName", serviceName }
            };
            LogFileLineage = logFileLineage;
        }

        public LogRecordSentinel ToLogRecordCdoc(
    string eventXml,
    string serviceName,
    string level = "",
    string task = "",
    string opCode = "",
    int processId = 0,
    int threadId = 0)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eventXml))
                {
                    throw new ArgumentNullException(nameof(eventXml));
                }

                var sanitizedXmlString = XmlVerification.VerifyAndRepairXml(eventXml);

                var xe = XElement.Parse(sanitizedXmlString);
                var eventData = xe.Element(ElementNames.EventData);
                var userData = xe.Element(ElementNames.UserData);

                var header = xe.Element(ElementNames.System);
                var recordId = long.Parse(header.Element(ElementNames.EventRecordId).Value);

                var systemPropertiesDictionary = CommonXmlFunctions.ConvertSystemPropertiesToDictionary(xe);

                var namedProperties = new Dictionary<string, string>();
                var dataWithoutNames = new List<string>();

                // Convert the EventData to named properties
                if (userData != null)
                {
                    namedProperties = CommonXmlFunctions.ParseUserData(userData).ToDictionary(x => x.Key, x => x.Value.ToString());
                }

                if (eventData != null)
                {
                    var eventDataProperties = CommonXmlFunctions.ParseEventData(eventData);
                    namedProperties = eventDataProperties.ToDictionary(x => x.Key, x => x.Value.ToString());
                }

                string json;
                if (dataWithoutNames.Count > 0)
                {
                    if (namedProperties.Count > 0)
                    {
                        throw new Exception("Event that has both unnamed and named data?");
                    }

                    json = JsonConvert.SerializeObject(dataWithoutNames, Formatting.Indented);
                }
                else
                {
                    json = JsonConvert.SerializeObject(namedProperties, Formatting.Indented);
                }

                var collectorTimestamp = DateTime.UtcNow;
                var logFileLineage = new Dictionary<string, object>
                {
                    { "UploadMachine", Environment.MachineName },
                    { "CollectorTimeStamp", collectorTimestamp },
                    { "CollectorUnixTimeStamp", collectorTimestamp.GetUnixTime() },
                    { "ServiceName", serviceName }
                };

                string[] executionProcessThread;
                if (systemPropertiesDictionary.ContainsKey("Execution"))
                {
                    executionProcessThread = systemPropertiesDictionary["Execution"].ToString()
                        .Split(new[]
                        {
                            ':'
                        }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    executionProcessThread = new string[]
                    {
                        "0",
                        "0"
                    };
                }

                return new LogRecordSentinel()
                {
                    EventRecordId = Convert.ToInt64(systemPropertiesDictionary["EventRecordID"]),
                    TimeCreated = Convert.ToDateTime(systemPropertiesDictionary["TimeCreated"]),
                    Computer = systemPropertiesDictionary["Computer"].ToString(),
                    ProcessId = processId.Equals(0) ? Convert.ToInt32(executionProcessThread[0]) : processId,
                    ThreadId = processId.Equals(0) ? Convert.ToInt32(executionProcessThread[1]) : threadId,
                    Provider = systemPropertiesDictionary["Provider"].ToString(),
                    EventId = Convert.ToInt32(systemPropertiesDictionary["EventID"]),
                    Level = !level.Equals(string.Empty) ? systemPropertiesDictionary["Level"].ToString() : level,
                    Version = CommonXmlFunctions.GetSafeExpandoObjectValue(systemPropertiesDictionary, "Version"),
                    Channel = systemPropertiesDictionary["Channel"].ToString(),
                    Security = CommonXmlFunctions.GetSafeExpandoObjectValue(systemPropertiesDictionary, "Security"),
                    Task = !task.Equals(string.Empty) ? systemPropertiesDictionary["Task"].ToString() : task,
                    Opcode = opCode,
                    EventData = json,
                    LogFileLineage = logFileLineage
                };
            }
            catch (Exception ex)
            {
                Trace.TraceError($"WinLog.EventRecordConversion.ToJsonLogRecord() threw an exception: {ex}");
                return null;
            }
        }
    }

    /// <summary>
    ///     Class for enabling JSON parsing of events.
    /// </summary>
    public class JsonParseFilter
    {
        public JsonParseFilter()
        {
        }

        public JsonParseFilter(string eventId, string dataName, string contains)
        {
            EventId = eventId;
            DataName = dataName;
            Contains = contains;
        }

        public string EventId { get; set; }

        public string DataName { get; set; }

        public string Contains { get; set; }
    }

    /// <summary>
    ///     Class defining metadata about where and when a log file was collected.
    /// </summary>
    public class LogFileLineage
    {
        public string Collector { get; set; }

        public string UploadMachine { get; set; }

        public long LogFileId { get; set; }

        public long Seq { get; set; }

        public DateTime CollectorTimeStamp { get; set; }

        public string CollectorUnixTimeStamp { get; set; }

        public string ServiceName { get; set; }
    }
}