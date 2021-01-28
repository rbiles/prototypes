// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;

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