// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SentinelCost.Core
{
    using System;
    using System.Collections.Generic;

    public class CostApiPackage
    {
        public long PackageId { get; set; }

        public DateTime UploadDateTime { get; set; }

        public DateTime ProcessingDateTime { get; set; }

        public string UploadServer { get; set; }

        public string ProcessingServer { get; set; }

        public int Count { get; set; }

        public Guid PackageGuid { get; set; }
    }

    public class EventXmlItem : CostApiPackage
    {
        public string EventXml { get; set; }
    }

    public class EventXmlList : EventXmlItem
    {
        public List<EventXmlItem> EventXmlListItems { get; set; } = new List<EventXmlItem>();
    }

    public class EventDictionaryItem : CostApiPackage
    {
        public IDictionary<string, object> EventDictionary { get; set; }
    }

    public class EventDictionaryList : EventDictionaryItem
    {
        public List<EventDictionaryItem> EventDictionaryListItems { get; set; } = new List<EventDictionaryItem>();
    }
}