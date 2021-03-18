using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace PipelineCost.Core
{
    public class PipelineCostCommon
    {
        public static string GetServiceName()
        {
            string serviceName = string.Empty;

            if (WindowsServiceHelpers.IsWindowsService())
            {
                // We install the service in folder named 
                // by the registered service name. So just get the folder name and assume it is the 
                // service name
                string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                DirectoryInfo dir_info = new DirectoryInfo(directoryName);
                serviceName = dir_info.Name;
            }
            else
            {
                serviceName = Process.GetCurrentProcess().ProcessName;
            }

            return serviceName;
        }
    }
}
