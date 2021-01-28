// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalyticsOdsApiHarness
{
    public static class Global
    {
        public static string GetMachineFqdn()
        {
            string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName)) // if hostname does not already include domain name
            {
                hostName += domainName; // add the domain name part
            }

            return hostName; // return the fully qualified name
        }
    }
}