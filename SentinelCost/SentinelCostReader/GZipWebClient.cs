// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Net;

namespace PipelineCost.Agent
{
    public class GZipWebClient : WebClient
    {
        internal static WebRequest GetWebRequest(Uri address)
        {
            var req = WebRequest.Create(address.OriginalString);
            HttpWebRequest request = (HttpWebRequest)req;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }
    }
}