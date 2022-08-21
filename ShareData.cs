using System;
using ExileCore;
using System.IO;
using System.Net;
using System.Text;
using ExileCore.Shared;
using System.Threading;
using System.Net.Sockets;
using System.Collections;
using ExileCore.Shared.Helpers;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ShareData
{

    public class ShareData : BaseSettingsPlugin<ShareDataSettings>
    {

        public Server ServerInstance = null;
        private static bool ServerIsRunning = false;
        private const int DefaultServerPort = 50000;

        // TODO: Create preaload as another instance
        private string PreloadAlerts => Path.Combine(DirectoryFullName, "config", "preload_alerts_default.txt");
        private string PreloadAlertsPersonal => Path.Combine(DirectoryFullName, "config", "preload_alerts_personal.txt");

        public void InitReloadFilesMethods() 
        {
            DataBuilder.ReloadGameFilesMethods.Add("LoadFiles");
            DataBuilder.ReloadGameFilesMethods.Add("ReloadFiles");
        }

        public void ReadConfigFiles()
        {
            if (!File.Exists(PreloadAlerts))
            {
                DebugWindow.LogError($"PreloadAlert.ReadConfigFiles -> Config file is missing: {PreloadAlerts}");
                return;
            }
            if (!File.Exists(PreloadAlertsPersonal))
            {
                File.Create(PreloadAlertsPersonal);
                DebugWindow.LogMsg($"PreloadAlert.ReadConfigFiles -> Personal config file got created: {PreloadAlertsPersonal}");
            }

            DataBuilder.PreloadConfigLines.Clear();

            AddLinesFromFile(PreloadAlerts, DataBuilder.PreloadConfigLines);
            AddLinesFromFile(PreloadAlertsPersonal, DataBuilder.PreloadConfigLines);
        }

        public static void AddLinesFromFile(string path, IDictionary<string, PreloadConfigLine> preloadLines)
        {
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#")) continue;

                var lineContent = line.Split(';');
                var metadataKey = lineContent[0].Trim();
                if (preloadLines.ContainsKey(metadataKey))
                {
                    if (line.StartsWith("-"))
                    {
                        preloadLines.Remove(metadataKey);
                    }
                    continue;
                }

                var textAndColor = new PreloadConfigLine
                {
                    Text = lineContent[1].Trim(),
                    Color = lineContent.ConfigColorValueExtractor(2)
                };
                preloadLines.Add(metadataKey, textAndColor);
            }
        }

        public override void OnLoad()
        {
            base.OnLoad();
            ReadConfigFiles();
            InitReloadFilesMethods();
        }

        public override bool Initialise()
        {
            GameController.LeftPanel.WantUse(() => Settings.Enable);

            int ServerPort = GetServerPort();
            ServerInstance = new Server(ServerPort);

            var dataUpdateCoroutine = new Coroutine(DataUpdateEvent(), this);
            var serverRestartCoroutine = new Coroutine(ServerRestartEvent(), this);
            Core.ParallelRunner.Run(dataUpdateCoroutine);
            Core.ParallelRunner.Run(serverRestartCoroutine);

            return true;
        }

        private int GetServerPort()
        {
            int Port = DefaultServerPort;

            try
            {
                int ParsedPort = int.Parse(Settings.Port.Value);

                if (1025 <= ParsedPort && ParsedPort < 65535)
                {
                    Port = ParsedPort;
                }
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"{nameof(ShareData)} -> {e}, default port is {DefaultServerPort}...");
                Settings.Port.SetValueNoEvent($"{DefaultServerPort}");
            }

            return Port;
        }

        private void RunServer()
        {
            try
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(ServerInstance.RunServer));
            }
            catch (Exception e) {
                DebugWindow.LogError($"{nameof(ShareData)}. Cant't run server with exception -> {e}");
            }
        }


        private IEnumerator DataUpdateEvent()
        {
            while (true)
            {
                try
                {
                    if (GameController != null)
                    {
                        DataBuilder.UpdateContentData(GameController);
                    }
                }
                catch (Exception e)
                {
                    DebugWindow.LogError($"{nameof(ShareData)} can't update content data {e}");
                }
                yield return new WaitTime(500);
            }
        }

        private IEnumerator ServerRestartEvent()
        {
            while (true) 
            {
                
                if (GameController != null)
                {
                    if (!ServerIsRunning)
                    {
                        RunServer();
                    }
                }

                yield return new WaitTime(500);
            }
        }


        public class Client
        {

            private void SendHeaders(TcpClient Client, int StatusCode, string StatusCodeSuffix)
            {
                string Headers = (
                    $"HTTP/1.1 {StatusCode} {StatusCodeSuffix}\n" +
                    "Content-Type: application/json\n" +
                    "Connection: Keep-Alive\n" +
                    "Keep-Alive: timeout=15\n\n"
                );
                byte[] HeadersBuffer = Encoding.UTF8.GetBytes(Headers);
                Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);
            }

            private void SendRequest(TcpClient Client, string Content, int StatusCode, string StatusCodeSuffix = "")
            {

                SendHeaders(Client, StatusCode, StatusCodeSuffix);

                byte[] Buffer = Encoding.UTF8.GetBytes(Content);
                Client.GetStream().Write(Buffer, 0, Buffer.Length);
                Client.Close();
            }

            private string ParseRequest(TcpClient Client)
            {
                string Request = "";
                byte[] Buffer = new byte[1024];
                int Count;

                while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
                {
                    Request += Encoding.UTF8.GetString(Buffer, 0, Count);

                    if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > 4096)
                    {
                        break;
                    }
                }

                Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");

                if (ReqMatch == Match.Empty)
                {
                    return "";
                }

                return ReqMatch.Groups[1].Value;
            }

            public Client(TcpClient Client)
            {
                try
                {
                    string RequestUri = ParseRequest(Client);
                    RequestUri = Uri.UnescapeDataString(RequestUri);

                    switch (RequestUri)
                    {
                        case "/get_content":
                            SendRequest(
                                Client,
                                DataBuilder.ContentAsJson(), 200, "OK"
                            );
                            break;
                        case "/":
                            SendRequest(Client, "{\"message\": \"error\"}", 404);
                            break;
                    }
                    Client.Close();
                }
                catch (Exception e) {
                    DebugWindow.LogError($"{nameof(Client)} in Client -> {e}");
                    Client.Close();
                }
            }
        }

        public class Server
        {
            TcpListener Listener;
            int Port;

            public Server(int ServerPort)
            {
                Port = ServerPort;
            }

            public void RunServer(Object StateInfo)
            {
                try
                {
                    Listener = new TcpListener(IPAddress.Any, Port);
                    Listener.Start();
                    
                    ServerIsRunning = true;
                    
                    while (ServerIsRunning)
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), Listener.AcceptTcpClient());
                    }

                    Listener.Stop();
                }
                catch (Exception e) {
                    DebugWindow.LogError($"{nameof(Server)} was crashed -> {e}");
                    using (StreamWriter sw = new StreamWriter("ShareDataCrashLog")) 
                    {
                        sw.Write($"{nameof(Server)} was crushed -> {e}");
                    }
                    ServerIsRunning = false;
                    Listener.Stop();
                    return;
                }
            }

            public void ClientThread(Object StateInfo)
            {
                try
                {
                    new Client((TcpClient)StateInfo);
                }
                catch (Exception e) {
                    DebugWindow.LogError($"{nameof(Server)} ClientThread was crushed -> {e}");
                }
            }
        }
    }
}
