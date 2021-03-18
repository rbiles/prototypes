// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SentinelCost.WebApi
{
    using System;
    using System.Diagnostics;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using SentinelCost.Core;

    public class Program
    {
        private static string categoryName = "WECEvents";
        private static string categoryHelp = "WEC Events processed in real time";

        public static IServiceProvider ServiceProvider { get; private set; }

        public static void Main(string[] args)
        {
            PerformanceCounters.CreateSentinelCostPerformanceCounters(categoryName, categoryHelp, true);

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
    }
}