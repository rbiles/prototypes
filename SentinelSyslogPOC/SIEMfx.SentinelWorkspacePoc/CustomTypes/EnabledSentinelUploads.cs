// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.CustomTypes
{
    using System;

    public class EnabledSentinelUploads
    {
        public bool WindowsEventsXmlFile { get; set; }

        public bool WindowsEventsFolderContents { get; set; }

        public bool SyslogToCustomLog { get; set; }

        public bool SyslogToLinuxSyslog { get; set; }

        public bool SyslogToCefSyslog { get; set; }

        public bool ContainerLog { get; set; }

        public bool LoadSecurityEventLog { get; set; }

        public bool CefFilesToSentinelProcessor { get; set; }

        public string CefFileFolderToUpload { get; set; }
    }
}