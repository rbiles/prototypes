// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace LogAnalyticsOdsApiHarness
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reactive.Kql;
    using System.Reflection;
    using System.ServiceProcess;
    using System.Threading;
    using System.Timers;
    using global::LogAnalyticsOdsApiHarness.CustomTypes;
    using Newtonsoft.Json;

    public class LogAnalyticsOdsApiHarness : ServiceBase
    {
        private System.Timers.Timer HeartbeatTimer { get; set; }

        public LogAnalyticsOdsApiHarness()
        {
            // The constructor for the service
        }

        protected override void OnStart(string[] args)
        {
            this.HeartbeatTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = TimeSpan.FromSeconds(5).TotalMilliseconds
            };
            this.HeartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            this.HeartbeatTimer.Start();
        }

        private void HeartbeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                this.ExecuteHeartbeatOperation();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                this.HeartbeatTimer.Start();
            }
        }

        [KqlScalarFunction("getprocessname")]
        public static string GetProcessName(uint pid)
        {
            string processName = Process.GetProcesses().FirstOrDefault(pr => pr.Id == pid)?.ProcessName;
            return string.IsNullOrEmpty(processName) ? pid.ToString() : processName;
        }

        [KqlScalarFunction("ntohs")]
        public static int NetworkToHostPort(ushort port)
        {
            short res = IPAddress.NetworkToHostOrder((short) port);
            string paddedRes = Convert.ToString(res, 2);
            paddedRes.PadLeft(8, '0');
            return Convert.ToInt32(paddedRes, 2);
        }

        private void ExecuteHeartbeatOperation()
        {
            string operationAction = ConfigurationManager.AppSettings["OperationAction"].ToLower();

            //TODO: Perform service startup actions
            if (operationAction.ToLower().Equals("containerlog"))
            {
                ContainerLogNew.InsertSampleDataSet();
            }
            else if (operationAction.ToLower().Equals("dhcp"))
            {
                DhcpLogSample.SendDataToODS_DhcpLog();
            }
            else if (operationAction.ToLower().Equals("evtx"))
            {
                EvtxLogSample.UploadFolderContents();
            }
            else
            {
                StartEtwListenerInstances();
            }
        }

        private void StartEtwListenerInstances()
        {
            // Get the current Sentinel config
            string configurationFile = ConfigurationManager.AppSettings["SentinelApiConfig"];
            bool useEventIngest = false;

            GlobalLog.WriteToStringBuilderLog($"Loading config [{configurationFile}].", 14001);
            string textOfJsonConfig = File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{configurationFile}"));
            SentinelApiConfig sentinelApiConfig = JsonConvert.DeserializeObject<SentinelApiConfig>(textOfJsonConfig);

            List<EtwListener> etwListeners = new List<EtwListener>();

            // Add custom local functions to Rx.Kql
            ScalarFunctionFactory.AddFunctions(typeof(LogAnalyticsOdsApiHarness));

            string etwConfigurationFile = "EtwConfig-DNS-TCP.json";

            GlobalLog.WriteToStringBuilderLog($"Loading ETW config [{etwConfigurationFile}].", 14001);
            string textOfEtwConfigurationFile = File.ReadAllText(Path.Combine(LogAnalyticsOdsApiHarness.GetExecutionPath(), $"{etwConfigurationFile}"));
            List<EtwListenerConfig> listEtwListenerConfigs = JsonConvert.DeserializeObject<List<EtwListenerConfig>>(textOfEtwConfigurationFile);

            foreach (EtwListenerConfig config in listEtwListenerConfigs)
            {
                etwListeners.Add(new EtwListener(sentinelApiConfig, config, useEventIngest));
            }

            // Wait for the process to end
            Thread.Sleep(Timeout.Infinite);
        }

        protected override void OnStop()
        {
            // TODO: Cleanup any code or run shutdown logic
        }

        public void ManualStart(string[] args)
        {
            // TODO: This is called from the Program.cs file, when debugging or starting from a command line
            this.OnStart(args);
        }

        public void ManualStop()
        {
            // TODO: This is called from the Program.cs file, when debugging or stopping from a command line
            this.OnStop();
        }

        public static string GetExecutionPath()
        {
            // deserialize JSON to the runtime type, and iterate.
            var path = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(path);
            return directory;
        }
    }
}