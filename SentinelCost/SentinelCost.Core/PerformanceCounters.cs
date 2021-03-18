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

    public enum PerfCounterAction
    {
        Increment = 0,
        Decrement
    }

    public static class PerformanceCounters
    {
        private static List<PerformanceCounterWindowsEvent> listPerformanceCounterWindowsEvents { get; set; } = new List<PerformanceCounterWindowsEvent>
        {
            new PerformanceCounterWindowsEvent
                { CounterName = "XML Events Sent", CounterHelp = "Windows Events Sent (XML)", PerformanceCounterType = PerformanceCounterType.NumberOfItems64 },
            new PerformanceCounterWindowsEvent
                { CounterName = "XML Events Received", CounterHelp = "Windows Events Received (XML)", PerformanceCounterType = PerformanceCounterType.NumberOfItems64 },
            new PerformanceCounterWindowsEvent
                { CounterName = "Dictionary Events Sent", CounterHelp = "Windows Events Sent (Dictionary)", PerformanceCounterType = PerformanceCounterType.NumberOfItems64 },
            new PerformanceCounterWindowsEvent
            {
                CounterName = "Dictionary Events Received", CounterHelp = "Windows Events Received (Dictionary)",
                PerformanceCounterType = PerformanceCounterType.NumberOfItems64
            },
            new PerformanceCounterWindowsEvent
            {
                CounterName = "XML Parsing Efficiency", CounterHelp = "Windows Events XML Parsing Efficiency", PerformanceCounterType = PerformanceCounterType.NumberOfItems64
            },
            new PerformanceCounterWindowsEvent
            {
                CounterName = "Dictionary Parsing Efficiency", CounterHelp = "Windows Events Dictionary Parsing Efficiency",
                PerformanceCounterType = PerformanceCounterType.NumberOfItems64
            },
            new PerformanceCounterWindowsEvent
            {
                CounterName = "XML Upload Efficiency", CounterHelp = "Windows Events XML Upload Efficiency", PerformanceCounterType = PerformanceCounterType.NumberOfItems64
            },
            new PerformanceCounterWindowsEvent
            {
                CounterName = "Dictionary Upload Efficiency", CounterHelp = "Windows Events Dictionary Upload Efficiency",
                PerformanceCounterType = PerformanceCounterType.NumberOfItems64
            }
        };

        public static void CreateSentinelCostPerformanceCounters(string categoryName, string categoryHelp, bool delete = false)
        {
            if (delete && PerformanceCounterCategory.Exists(categoryName))
            {
                PerformanceCounterCategory.Delete(categoryName);
            }

            // Create category.
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                CounterCreationDataCollection counters = new CounterCreationDataCollection();
                foreach (PerformanceCounterWindowsEvent performanceCounterWindowsEvent in listPerformanceCounterWindowsEvents)
                {
                    counters.Add(new CounterCreationData(performanceCounterWindowsEvent.CounterName, performanceCounterWindowsEvent.CounterHelp,
                        performanceCounterWindowsEvent.PerformanceCounterType));
                }

                PerformanceCounterCategory.Create(categoryName, categoryHelp, PerformanceCounterCategoryType.SingleInstance, counters);
            }
        }
    }

    public class PerformanceCounterWindowsEvent
    {
        public string CounterName { get; set; }

        public string CounterHelp { get; set; }

        public PerformanceCounterType PerformanceCounterType { get; set; }
    }
}