﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HelloWorldWPF.CustomTypes;
using Newtonsoft.Json;

namespace HelloWorldWPF
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

       
        private void buttonPost_Click(object sender, RoutedEventArgs e)
        {
            HelloWorld_Post();
        }

        private async Task HelloWorld_Post()
        {
            try
            {
                // Get a random server name from the configured list
                var receiverServer = "localhost";
                var receiverPort = "5000";

                var uploadStopWatch = Stopwatch.StartNew();

                // Create a client, and add the authentication cert
                var _clientHandler = new HttpClientHandler();
                var Client = new HttpClient(_clientHandler);

                var helloWorldItem = new HelloWorldItem
                {
                    Id = 10000,
                    Name = "HelloWorldFromClient",
                    IsComplete = false
                };

                // Build the client data for the file metrics
                using (var content = new MultipartFormDataContent())
                {
                    try
                    {
                        var uri = $"http://{receiverServer}:{receiverPort}/api/HelloWorld";

                        var parameters = JsonConvert.SerializeObject(helloWorldItem);

                        var req = WebRequest.Create(uri);

                        req.Method = "POST";
                        req.ContentType = "application/json";

                        var bytes = Encoding.ASCII.GetBytes(parameters);

                        req.ContentLength = bytes.Length;

                        using (var os = req.GetRequestStream())
                        {
                            os.Write(bytes, 0, bytes.Length);

                            os.Close();
                        }

                        var stream = req.GetResponse().GetResponseStream();

                        if (stream != null)
                            using (stream)
                            using (var sr = new StreamReader(stream))
                            {
                                var streamResult = sr.ReadToEnd().Trim();

                                var returnHelloWorldItem = JsonConvert.DeserializeObject<HelloWorldItem>(streamResult);

                                Console.WriteLine(
                                    $"Processed - Return from server: {returnHelloWorldItem.TimeOfHello}  Message: [{returnHelloWorldItem.ReturnMessage}].");

                                listviewHelloWorldResults.Items.Add(returnHelloWorldItem);
                            }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    Client.Dispose();
                }

                uploadStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void buttonGet_Click(object sender, RoutedEventArgs e)
        {
            HelloWorld_Get();
        }

        private async Task HelloWorld_Get()
        {
            try
            {
                // Get a random server name from the configured list
                var receiverServer = "localhost";
                var receiverPort = "5000";

                var uploadStopWatch = Stopwatch.StartNew();

                // Create a client, and add the authentication cert
                var _clientHandler = new HttpClientHandler();
                var Client = new HttpClient(_clientHandler);

                // Build the client data for the file metrics
                using (var content = new MultipartFormDataContent())
                {
                    try
                    {
                        var uri = $"http://{receiverServer}:{receiverPort}/api/HelloWorld";

                        var req = WebRequest.Create(uri);

                        req.Method = "GET";
                        req.ContentType = "application/json";

                        var stream = req.GetResponse().GetResponseStream();

                        if (stream != null)
                            using (stream)
                            using (var sr = new StreamReader(stream))
                            {
                                var streamResult = sr.ReadToEnd().Trim();

                                var returnHelloWorldItem = JsonConvert.DeserializeObject<HelloWorldItem>(streamResult);

                                Console.WriteLine(
                                    $"Processed - Return from server: {returnHelloWorldItem.TimeOfHello}  Message: [{returnHelloWorldItem.ReturnMessage}].");

                                listviewHelloWorldResults.Items.Add(returnHelloWorldItem);
                            }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    Client.Dispose();
                }

                uploadStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}