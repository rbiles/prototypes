// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var serviceToRun = new SentinelWorkspacePoc();

                if (args.Length > 0 && args.Contains("standalone", StringComparer.OrdinalIgnoreCase))
                {
                    void cancelAction(object o, ConsoleCancelEventArgs e)
                    {
                        serviceToRun.ManualStop();
                        Thread.Sleep(200);
                    }

                    Console.CancelKeyPress += cancelAction;
                    serviceToRun.ManualStart(args);
                    Thread.Sleep(Timeout.Infinite);
                }
                else
                {
                    ServiceBase.Run(serviceToRun);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}