// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using WinLog;
using WinLog.Helpers;

namespace PipelineCost.Agent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Eventing.Reader;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using PipelineCost.Core;
    using SentinelCost.Core;

    class Program
    {
        public static TimeSpan MaxSkipTime = TimeSpan.FromMinutes(5);
        public static int MaxSkipCount = 10 * 1000 * 1000;

        private static readonly bool sendInBatches = true;
        private static readonly int batchItemCount = 500;

        private static string logName;
        private static readonly TimeSpan MaxLingerTimeSpan = TimeSpan.Parse("00:00:15");
        private static DateTime lastSendDateTime = DateTime.UtcNow;

        private static EventXmlList eventXmlList = new EventXmlList();
        private static EventDictionaryList eventDictionaryList = new EventDictionaryList();
        private static DataType dataType;

        private static CompressionType CompressionType;

        private static readonly string categoryName = "WECEvents";
        private static readonly string categoryHelp = "WEC Events processed in real time";
        private static readonly string countersName = "Events Sent";
        private static string counterHelp = "WECEvents Sent";

        private static CancellationTokenSource cancellationTokenSource;
        private static CancellationToken cancellationToken;

        public static Guid CurrentPackageGuid { get; set; } = Guid.NewGuid();

        public static ConfigHelper ConfigHelperObject { get; set; }

        static void Main(string[] args)
        {
            PerformanceCounters.CreateSentinelCostPerformanceCounters(categoryName, categoryHelp);

            //_logName = args[0];
            //Enum.TryParse(args[1], out dataType);

            ConfigHelperObject = new ConfigHelper();
            bool resetListener = true;

            ///  Run the console app
            ConsoleKeyInfo cki;
            Console.WriteLine("Press any combination of CTL.");
            Console.WriteLine("Press the Escape (Esc) key to quit: \n");
            Task runTask = null;

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            do
            {
                // Run importing in background
                if (runTask == null)
                {
                    logName = GetEventLogSelection();
                    dataType = GetDataTypeSelection();
                    CompressionType = GetCompressionSelection();
                    Console.WriteLine("Press any CTL+B to make a different selection.");
                    runTask = Task.Run(() => Subscribe(logName), cancellationToken);
                }

                cki = Console.ReadKey();

                if ((cki.Modifiers & ConsoleModifiers.Control) != 0 && cki.Key == ConsoleKey.B)
                {
                    Console.Write("CTL+");
                    Console.WriteLine($"Stop reading {logName}  task...");

                    cancellationTokenSource.Cancel();
                    runTask = null;
                }
            }
            while (cki.Key != ConsoleKey.Escape);
        }

        private static void PrintToConsole(string message)
        {
            //Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine(message);
        }

        private static string GetEventLogSelection()
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine("Press number for EventLog to read.");

            int itemCounter = 0;
            foreach (EventLogName eventLog in (EventLogName[]) Enum.GetValues(typeof(EventLogName)))
            {
                Console.WriteLine($"\t{itemCounter}. {eventLog}");
                itemCounter++;
            }

            ConsoleKeyInfo cki;

            cki = Console.ReadKey();

            string selection = Enum.GetName(typeof(EventLogName), GetKeyValue(cki.KeyChar));

            if (selection == null)
            {
                selection = "Security";
                Console.WriteLine($" --- Out Of Range: [{selection}] will be used. ");
            }
            else
            {
                Console.WriteLine($" --- Selected: [{selection}] ");
            }

            Console.ForegroundColor = clr;

            return selection;
        }

        private static DataType GetDataTypeSelection()
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("Press number for DataType to upload.");

            int itemCounter = 0;
            foreach (DataType eventLog in (DataType[]) Enum.GetValues(typeof(DataType)))
            {
                Console.WriteLine($"\t{itemCounter}. {eventLog}");
                itemCounter++;
            }

            ConsoleKeyInfo cki;

            cki = Console.ReadKey();

            var dt = (DataType) GetKeyValue(cki.KeyChar);
            switch (dt)
            {
                case DataType.Xml:
                case DataType.Dictionary:
                    Console.WriteLine($" --- Selected: [{dt}] ");
                    break;

                default:
                    dt = DataType.Xml;
                    Console.WriteLine($" --- Out Of Range: [{dt}] will be used. ");
                    break;
            }

            Console.ForegroundColor = clr;
            return dt;
        }

        private static CompressionType GetCompressionSelection()
        {
            ConsoleColor clr = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("Press number for CompressionType to upload.");

            int itemCounter = 0;
            foreach (CompressionType eventLog in (CompressionType[]) Enum.GetValues(typeof(CompressionType)))
            {
                Console.WriteLine($"\t{itemCounter}. {eventLog}");
                itemCounter++;
            }

            ConsoleKeyInfo cki;

            cki = Console.ReadKey();

            var ct = (CompressionType) GetKeyValue(cki.KeyChar);
            switch (ct)
            {
                case CompressionType.Compressed:
                    Console.WriteLine($" --- Selected: [{ct}] ");
                    break;

                default:
                    ct = CompressionType.Clear;
                    Console.WriteLine($" --- Out Of Range: [{ct}] will be used. ");
                    break;
            }

            Console.ForegroundColor = clr;
            return ct;
        }

        private static int GetKeyValue(int keyValue)
        {
            if (keyValue >= 48 && keyValue <= 57)
            {
                return keyValue - 48;
            }
            else if (keyValue >= 96 && keyValue <= 105)
            {
                return keyValue - 96;
            }
            else
            {
                return -1; // Not a number... do whatever...
            }
        }

        public static void ExceptionHandler(Exception ex)
        {
            PrintToConsole($"{ex}");
        }

        public static void Subscribe(string logToListen)
        {
            EventLogWatcher watcher = null;
            try
            {
                EventLogQuery subscriptionQuery = new EventLogQuery(
                    logToListen, PathType.LogName, null);

                watcher = new EventLogWatcher(subscriptionQuery, null, true);

                // Make the watcher listen to the EventRecordWritten
                // events.  When this event happens, the callback method
                // (EventLogEventRead) is called.
                watcher.EventRecordWritten +=
                    new EventHandler<EventRecordWrittenEventArgs>(
                        EventLogEventRead);

                // Activate the subscription
                watcher.Enabled = true;

                Thread.Sleep(Timeout.Infinite);
            }
            catch (EventLogReadingException e)
            {
            }
            finally
            {
                // Stop listening to events
                watcher.Enabled = false;

                if (watcher != null)
                {
                    watcher.Dispose();
                }
            }
        }

        // Callback method that gets executed when an event is
        // reported to the subscription.
        public static void EventLogEventRead(object obj,
            EventRecordWrittenEventArgs arg)
        {
            // Make sure there was no error reading the event.
            if (arg.EventRecord != null)
            {
                switch (dataType)
                {
                    case DataType.Xml:
                        var eventXml = XmlVerification.VerifyAndRepairXml(arg.EventRecord.ToXml());
                        SendXml(eventXml);
                        break;

                    case DataType.Dictionary:
                        var eventDynamic = LogReader.ParseEvent(arg.EventRecord);
                        SendDictionary(eventDynamic);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void SendDictionary(IDictionary<string, object> eventDynamic)
        {
            string counterName = "Dictionary Events Sent";
            if (sendInBatches)
            {
                eventDictionaryList.EventDictionaryListItems.Add(new EventDictionaryItem
                {
                    EventDictionary = eventDynamic,
                    UploadServer = Environment.MachineName,
                    UploadDateTime = DateTime.UtcNow,
                    PackageGuid = CurrentPackageGuid
                });

                if (eventDictionaryList.EventDictionaryListItems.Count == batchItemCount || DateTime.UtcNow - lastSendDateTime >= MaxLingerTimeSpan)
                {
                    PerformanceCounterManagement(batchItemCount, PerfCounterAction.Increment, counterName);
                    SendEventDictionaryList(eventDictionaryList).ConfigureAwait(false);
                    eventDictionaryList = new EventDictionaryList();
                    PerformanceCounterManagement(batchItemCount, PerfCounterAction.Decrement, counterName);
                    lastSendDateTime = DateTime.UtcNow;
                }
            }
            else
            {
                SendEventDictionary(eventDynamic).ConfigureAwait(false);
            }
        }

        private static void PerformanceCounterManagement(int eventsProcessed, PerfCounterAction addValue, string counterName)
        {
            PerformanceCounter eventPerformanceCounter = new PerformanceCounter(categoryName, counterName);
            eventPerformanceCounter.ReadOnly = false;

            if (addValue == PerfCounterAction.Increment)
            {
                eventPerformanceCounter.IncrementBy(eventsProcessed);
            }
            else
            {
                eventPerformanceCounter.IncrementBy(-1 * eventsProcessed);
            }
        }

        private static void SendXml(string eventXml)
        {
            string counterName = "XML Events Sent";

            if (sendInBatches)
            {
                eventXmlList.EventXmlListItems.Add(new EventXmlItem
                {
                    EventXml = eventXml,
                    UploadServer = Environment.MachineName,
                    UploadDateTime = DateTime.UtcNow
                });

                if (eventXmlList.EventXmlListItems.Count == batchItemCount || DateTime.UtcNow - lastSendDateTime >= MaxLingerTimeSpan)
                {
                    PerformanceCounterManagement(batchItemCount, PerfCounterAction.Increment, counterName);
                    SendEventXmlList(eventXmlList).ConfigureAwait(false);
                    eventXmlList = new EventXmlList();
                    PerformanceCounterManagement(batchItemCount, PerfCounterAction.Decrement, counterName);
                    lastSendDateTime = DateTime.UtcNow;
                }
            }
            else
            {
                SendEventXml(eventXml).ConfigureAwait(false);
            }
        }

        private static async Task SendEventXml(string eventXml)
        {
            bool uploadSuccessful;

            try
            {
                // Get a random server name from the configured list
                string receiverServer = "localhost";
                string receiverPort = "5000";

                Stopwatch uploadStopWatch = Stopwatch.StartNew();

                // Create a client, and add the authentication cert
                var _clientHandler = new HttpClientHandler();
                HttpClient Client = new HttpClient(_clientHandler);

                var eventXmlItem = new EventXmlItem
                {
                    PackageId = 0,
                    EventXml = eventXml,
                    UploadServer = Environment.MachineName,
                    UploadDateTime = DateTime.UtcNow
                };

                // Build the client data for the file metrics
                using (var content = new MultipartFormDataContent())
                {
                    try
                    {
                        string uri = $"http://{receiverServer}:{receiverPort}/api/EventXml";

                        var parameters = Newtonsoft.Json.JsonConvert.SerializeObject(eventXmlItem);

                        var req = WebRequest.Create(uri);

                        req.Method = "POST";
                        req.ContentType = "application/json";

                        byte[] bytes = Encoding.ASCII.GetBytes(parameters);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        var stream = req.GetResponse().GetResponseStream();

                        if (stream != null)
                        {
                            using (stream)
                            using (var sr = new StreamReader(stream))
                            {
                                string streamResult = sr.ReadToEnd().Trim();

                                var returnEventXmlItem = JsonConvert.DeserializeObject<EventXmlItem>(streamResult);

                                PrintToConsole(
                                    $"Processed {returnEventXmlItem.Count} record(s) in {(returnEventXmlItem.ProcessingDateTime - returnEventXmlItem.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler(ex);
                        uploadSuccessful = false;
                    }

                    Client.Dispose();
                }

                uploadStopWatch.Stop();

                List<SentinelCostMetric> metricList = new List<SentinelCostMetric>
                {
                    new SentinelCostMetric
                    {
                        MachineName = Environment.MachineName,
                        ServiceName = ConfigHelperObject.ServiceName,
                        OccurenceUtc = DateTime.UtcNow,
                        PackageGuid = eventDictionaryList.EventDictionaryListItems[0].PackageGuid,
                        PackageId = eventDictionaryList.EventDictionaryListItems[0].PackageId,
                        EventType = Enum.GetName(typeof(EventType), EventType.Send),
                        MetricData = new Dictionary<string, object>()
                        {
                            { "UploadTime", ConfigHelperObject.GetStopWatchDictionary(uploadStopWatch) }
                        }
                    }
                };
                ConfigHelperObject.LoadMetricsToKusto("Metrics", metricList);
            }
            catch (Exception ex)
            {
                ExceptionHandler(ex);
            }
        }

        private static async Task SendEventXmlList(EventXmlList eventXmlItems)
        {
            bool uploadSuccessful;

            try
            {
                // Get a random server name from the configured list
                string receiverServer = "localhost";
                string receiverPort = "5000";

                Stopwatch sendStopWatch = Stopwatch.StartNew();

                eventXmlItems.UploadServer = Environment.MachineName;
                eventXmlItems.UploadDateTime = DateTime.UtcNow;

                // Build the client data for the file metrics
                try
                {
                    string uri = $"http://{receiverServer}:{receiverPort}/api/EventXmlList";

                    var parameters = JsonConvert.SerializeObject(eventXmlItems);

                    if (CompressionType == CompressionType.Compressed)
                    {
                        var req = GZipWebClient.GetWebRequest(new Uri(uri));

                        req.Method = "POST";
                        req.ContentType = "application/json";

                        req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

                        byte[] bytes = Encoding.ASCII.GetBytes(parameters);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        using (var str = req.GetResponse().GetResponseStream())
                        using (var sr = new StreamReader(str))
                        {
                            string streamResult = await sr.ReadToEndAsync();

                            var returnEventXmlList = JsonConvert.DeserializeObject<EventXmlList>(streamResult);

                            PrintToConsole(
                                $"Processed {returnEventXmlList.EventXmlListItems.Count} record(s) in {(returnEventXmlList.ProcessingDateTime - returnEventXmlList.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                        }
                    }
                    else
                    {
                        var req = WebRequest.Create(uri);

                        req.Method = "POST";
                        req.ContentType = "application/json";
                        byte[] bytes = Encoding.ASCII.GetBytes(parameters);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        var stream = req.GetResponse().GetResponseStream();

                        if (stream != null)
                        {
                            using (stream)
                            using (var sr = new StreamReader(stream))
                            {
                                string streamResult = sr.ReadToEnd().Trim();

                                var returnEventXmlList = JsonConvert.DeserializeObject<EventXmlList>(streamResult);

                                PrintToConsole(
                                    $"Processed {returnEventXmlList.EventXmlListItems.Count} record(s) in {(returnEventXmlList.ProcessingDateTime - returnEventXmlList.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    uploadSuccessful = false;
                }

                sendStopWatch.Stop();

                Stopwatch conversionStopwatch = Stopwatch.StartNew();
                List<LogRecordSentinel> listLogRecordCdocs =
                    eventXmlList.EventXmlListItems.Select(d => new PipelineCostConversion().ToLogRecordCdoc(d.EventXml, ConfigHelperObject.ServiceName)).ToList();
                conversionStopwatch.Stop();

                Stopwatch dataloadStopwatch = Stopwatch.StartNew();
                ConfigHelperObject.LoadDataToKusto("EventData", listLogRecordCdocs);
                dataloadStopwatch.Stop();

                List<SentinelCostMetric> metricList = new List<SentinelCostMetric>
                {
                    new SentinelCostMetric
                    {
                        MachineName = Environment.MachineName,
                        ServiceName = ConfigHelperObject.ServiceName,
                        OccurenceUtc = DateTime.UtcNow,
                        PackageGuid = eventXmlItems.EventXmlListItems[0].PackageGuid,
                        PackageId = eventXmlItems.EventXmlListItems[0].PackageId,
                        EventType = Enum.GetName(typeof(EventType), EventType.Send),
                        MetricData = new Dictionary<string, object>()
                        {
                            { "SendType", Enum.GetName(typeof(DataType), dataType) },
                            { "Send", ConfigHelperObject.GetStopWatchDictionary(sendStopWatch) },
                            { "Conversion", ConfigHelperObject.GetStopWatchDictionary(conversionStopwatch) },
                            { "DataLoad", ConfigHelperObject.GetStopWatchDictionary(dataloadStopwatch) }
                        }
                    }
                };
                ConfigHelperObject.LoadMetricsToKusto("Metrics", metricList);
            }
            catch (Exception ex)
            {
                ExceptionHandler(ex);
            }
        }

        private static async Task SendEventDictionary(IDictionary<string, object> eventDictionary)
        {
            bool uploadSuccessful;

            try
            {
                // Get a random server name from the configured list
                string receiverServer = "localhost";
                string receiverPort = "5000";

                Stopwatch uploadStopWatch = Stopwatch.StartNew();

                // Create a client, and add the authentication cert
                var _clientHandler = new HttpClientHandler();
                HttpClient Client = new HttpClient(_clientHandler);

                var eventDictionaryItem = new EventDictionaryItem
                {
                    PackageId = 0,
                    EventDictionary = eventDictionary,
                    UploadServer = Environment.MachineName,
                    UploadDateTime = DateTime.UtcNow
                };

                // Build the client data for the file metrics
                try
                {
                    string uri = $"http://{receiverServer}:{receiverPort}/api/EventDictionary";

                    var parameters = Newtonsoft.Json.JsonConvert.SerializeObject(eventDictionaryItem);

                    var req = WebRequest.Create(uri);

                    req.Method = "POST";
                    req.ContentType = "application/json";

                    byte[] bytes = Encoding.ASCII.GetBytes(parameters);

                    req.ContentLength = bytes.Length;

                    using (var os = req.GetRequestStream())
                    {
                        os.Write(bytes, 0, bytes.Length);

                        os.Close();
                    }

                    var stream = req.GetResponse().GetResponseStream();

                    if (stream != null)
                    {
                        using (stream)
                        using (var sr = new StreamReader(stream))
                        {
                            string streamResult = sr.ReadToEnd().Trim();

                            var returnEventDictionaryItem = JsonConvert.DeserializeObject<EventDictionaryItem>(streamResult);

                            PrintToConsole(
                                $"Processed {returnEventDictionaryItem.Count} record(s) in {(returnEventDictionaryItem.ProcessingDateTime - returnEventDictionaryItem.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                        }
                    }
                }
                catch (Exception e)
                {
                    uploadSuccessful = false;
                }

                Client.Dispose();

                uploadStopWatch.Stop();
            }
            catch (Exception ex)
            {
                ExceptionHandler(ex);
            }
        }

        private static async Task SendEventDictionaryList(EventDictionaryList eventDictionaryItems)
        {
            bool uploadSuccessful;

            try
            {
                // Get a random server name from the configured list
                string receiverServer = "localhost";
                string receiverPort = "5000";

                Stopwatch sendStopWatch = Stopwatch.StartNew();

                // Create a client, and add the authentication cert
                var _clientHandler = new HttpClientHandler();
                HttpClient Client = new HttpClient(_clientHandler);

                eventDictionaryItems.UploadServer = Environment.MachineName;
                eventDictionaryItems.UploadDateTime = DateTime.UtcNow;

                // Build the client data for the file metrics
                try
                {
                    string uri = $"http://{receiverServer}:{receiverPort}/api/EventDictionaryList";

                    var uploadContent = JsonConvert.SerializeObject(eventDictionaryItems);

                    if (CompressionType == CompressionType.Compressed)
                    {
                        var req = GZipWebClient.GetWebRequest(new Uri(uri));

                        req.Method = "POST";
                        req.ContentType = "application/json";

                        req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

                        byte[] bytes = Encoding.ASCII.GetBytes(uploadContent);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        using (var str = req.GetResponse().GetResponseStream())
                        using (var sr = new StreamReader(str))
                        {
                            string streamResult = await sr.ReadToEndAsync();

                            var returnEventDictionaryList = JsonConvert.DeserializeObject<EventDictionaryList>(streamResult);

                            PrintToConsole(
                                $"Processed {returnEventDictionaryList.EventDictionaryListItems.Count} record(s) in {(returnEventDictionaryList.ProcessingDateTime - returnEventDictionaryList.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                        }
                    }
                    else
                    {
                        var req = WebRequest.Create(uri);

                        req.Method = "POST";
                        req.ContentType = "application/json";
                        byte[] bytes = Encoding.ASCII.GetBytes(uploadContent);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        var stream = req.GetResponse().GetResponseStream();

                        if (stream != null)
                        {
                            using (stream)
                            using (var sr = new StreamReader(stream))
                            {
                                string streamResult = sr.ReadToEnd().Trim();

                                var returnEventDictionaryList = JsonConvert.DeserializeObject<EventDictionaryList>(streamResult);

                                PrintToConsole(
                                    $"Processed {returnEventDictionaryList.EventDictionaryListItems.Count} record(s) in {(returnEventDictionaryList.ProcessingDateTime - returnEventDictionaryList.UploadDateTime).TotalMilliseconds:N2} milliseconds.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    uploadSuccessful = false;
                }

                Client.Dispose();

                sendStopWatch.Stop();

                Stopwatch conversionStopwatch = Stopwatch.StartNew();
                List<LogRecordSentinel> listLogRecordCdocs = eventDictionaryItems.EventDictionaryListItems
                    .Select(d => new LogRecordSentinel(d.EventDictionary, null, ConfigHelperObject.ServiceName)).ToList();
                conversionStopwatch.Stop();

                Stopwatch dataloadStopwatch = Stopwatch.StartNew();
                ConfigHelperObject.LoadDataToKusto("EventData", listLogRecordCdocs);
                dataloadStopwatch.Stop();

                List<SentinelCostMetric> metricList = new List<SentinelCostMetric>
                {
                    new SentinelCostMetric
                    {
                        MachineName = Environment.MachineName,
                        ServiceName = ConfigHelperObject.ServiceName,
                        OccurenceUtc = DateTime.UtcNow,
                        PackageGuid = eventDictionaryItems.EventDictionaryListItems[0].PackageGuid,
                        PackageId = eventDictionaryItems.EventDictionaryListItems[0].PackageId,
                        EventType = Enum.GetName(typeof(EventType), EventType.Send),
                        MetricData = new Dictionary<string, object>()
                        {
                            { "SendType", Enum.GetName(typeof(DataType), dataType) },
                            { "Send", ConfigHelperObject.GetStopWatchDictionary(sendStopWatch) },
                            { "Conversion", ConfigHelperObject.GetStopWatchDictionary(conversionStopwatch) },
                            { "DataLoad", ConfigHelperObject.GetStopWatchDictionary(dataloadStopwatch) }
                        }
                    }
                };
                ConfigHelperObject.LoadMetricsToKusto("Metrics", metricList);
            }
            catch (Exception ex)
            {
                ExceptionHandler(ex);
            }
        }
    }
}