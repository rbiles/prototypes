// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using SIEMfx.SentinelWorkspacePoc.CustomTypes;

    public class LogAnalyticsPublicApi
    {
        public static async Task<bool> SendEventsToLogAnalytics(string events, SentinelApiConfig sentinelApiConfig, string workspaceKey)
        {
            await Task.Run(() =>
            {
                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(events);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring +
                                      "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, workspaceKey);
                string signature = "SharedKey " + sentinelApiConfig.WorkspaceId + ":" + hashedString;

                PostData(signature, datestring, events, sentinelApiConfig);
            });

            return true;
        }

        private static string BuildSignature(string message, string secret)
        {
            var encoding = new ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Send a request to the POST API endpoint
        private static async void PostData(string signature, string date, string we_json, SentinelApiConfig sentinelApiConfig)
        {
            try
            {
                await Task.Run(() =>
                {
                    string url = "https://" + sentinelApiConfig.WorkspaceId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Log-Type", sentinelApiConfig.LogName);
                    client.DefaultRequestHeaders.Add("Authorization", signature);
                    client.DefaultRequestHeaders.Add("x-ms-date", date);
                    // client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                    HttpContent httpContent = new StringContent(we_json, Encoding.UTF8);
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                    HttpContent responseContent = response.Result.Content;
                    string result = responseContent.ReadAsStringAsync().Result;
                    // Console.WriteLine("Return Result: " + result);
                });
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
    }
}