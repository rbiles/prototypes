// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace PipelineCost.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Eventing.Reader;
    using System.Linq;
    using System.Xml.Linq;
    using Newtonsoft.Json;
    using SentinelCost.Core;
    using WinLog;
    using WinLog.Helpers;

    /// <summary>
    ///     The Event Record Conversion class
    /// </summary>
    public class PipelineCostConversion : IDisposable
    {
        private readonly ProviderStringCache providerStringCache = new ProviderStringCache();

        public void Dispose()
        {
            // No actions to dispose of, yet...  Implemented to support the dispose pattern
        }

        /// <summary>
        ///     Creates a JsonLogRecord object
        /// </summary>
        /// <param name="eventXml"></param>
        /// <param name="serviceName"></param>
        /// <param name="level"></param>
        /// <param name="task"></param>
        /// <param name="opCode"></param>
        /// <param name="processId"></param>
        /// <param name="threadId"></param>
        /// <returns></returns>
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
}