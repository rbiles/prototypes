// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    public enum XmlCreationMechanism
    {
        StringReplacement = 0,
        XElement,
        XmlWriter,
        TypedUpload
    }

    public enum EtwListenerType
    {
        Dns = 0,
        Tcp = 1
    }

    public class GlobalLog : IDisposable
    {
        private static readonly List<string> sbLog = new List<string>();

        public static void WriteToStringBuilderLog(string logEntry, int errorNumber = 0)
        {
            string errorNumberString = errorNumber > 0 ? " - " + errorNumber : string.Empty;
            string message = $"{logEntry}{errorNumberString}";

            Console.WriteLine(message);
            sbLog.Add(logEntry);
        }

        public void Dispose()
        {
            string filePath = Path.Combine(GetExecutionPath(), $"LogFile_{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt");
            File.AppendAllLines(filePath, sbLog);
        }

        private static string GetExecutionPath()
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(path);
            return directory;
        }
    }
}