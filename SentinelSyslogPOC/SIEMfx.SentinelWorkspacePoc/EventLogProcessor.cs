// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Diagnostics.Eventing.Reader;
    using System.Reflection;

    public class EventLogProcessor
    {
        private string eventLog;

        public EventLogWatcher watcher;

        private Action<EventRecord> outputAction;

        public EventLogProcessor(string eventLog, Action<EventRecord> delegateOutput, bool readEventLogFileFromBeginning)
        {
            this.eventLog = eventLog;
            InvalidState = false;
            outputAction = delegateOutput;

            // Create the EventLogWatcher object, and register the listener for events
            string query = "*[System/Provider/@Name=\"Microsoft-Windows-Security-Auditing\"]";
            EventLogQuery eventLogQuery = new EventLogQuery(eventLog, PathType.LogName, query);
            watcher = new EventLogWatcher(eventLogQuery, null, readEventLogFileFromBeginning);
            watcher.EventRecordWritten += Watcher_EventRecordWritten;
        }

        public void InitializeEventLogWatcher()
        {
            if (!watcher.Enabled)
            {
                watcher.Enabled = true;
            }
        }

        public void Stop()
        {
            if (watcher != null)
            {
                watcher.EventRecordWritten -= Watcher_EventRecordWritten;
                watcher.Enabled = false;
            }
        }

        public bool InvalidState
        {
            get;
            set;
        }

        private void Watcher_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null)
            {
                InvalidState = true;
                return;
            }

            outputAction(e.EventRecord);
            //eventLog.OnNext(e.EventRecord as EventLogRecord);
        }

        public static EventBookmark CreateEventBookmark(string bookmarkString)
        {
            var constr = typeof(EventBookmark).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(string) },
                null);

            return (EventBookmark)constr.Invoke(new object[] { bookmarkString });
        }
    }
}