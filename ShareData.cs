using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using System.Text.RegularExpressions;

using ExileCore;
using ExileCore.Shared;

namespace ShareData
{

    public class ShareData : BaseSettingsPlugin<ShareDataSettings>
    {

        public required Server ServerInstance;
        private const int DefaultServerPort = 50000;
        private string PreloadAlerts => Path.Combine(DirectoryFullName, "config", "preload_alerts_default.txt");
        private string PreloadAlertsPersonal => Path.Combine(DirectoryFullName, "config", "preload_alerts_personal.txt");

        private bool ServerIsRunning
        {
            get
            {
                return ServerInstance.IsRunning;
            }
        }

        public override void OnLoad()
        {
            GameController.LeftPanel.WantUse(() => Settings.Enable);
            ServerInstance = new Server(GetServerPort());
        }

        public override bool Initialise()
        {
            try
            {
                PreloadBuilder.Initialise(PreloadAlerts, PreloadAlertsPersonal);

                var dataUpdateCoroutine = new Coroutine(DataUpdateEvent(), this);
                var serverControlCoroutine = new Coroutine(ServerControlEvent(), this);
                Core.ParallelRunner.Run(dataUpdateCoroutine);
                Core.ParallelRunner.Run(serverControlCoroutine);
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"{nameof(ShareData)} -> {e}, Cannot initialize plugin's entities...");
                return false;
            }

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
            if (ServerIsRunning) return;

            try
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback((_) => ServerInstance.RunServer()));
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"{nameof(ShareData)}. Cant't run server with exception -> {e}");
            }
        }

        private IEnumerator DataUpdateEvent()
        {
            WaitTime waitTime = new WaitTime(500);

            while (true)
            {
                if (!Settings.Enable)
                {
                    yield return waitTime;
                    continue;
                }
                if (GameController is null)
                {
                    DebugWindow.LogMsg($"{nameof(ShareData)} GameController is null");
                    yield return waitTime;
                    continue;
                }

                try
                {
                    DataBuilder.UpdateContentData(GameController);
                }
                catch (Exception e)
                {
                    DebugWindow.LogError($"{nameof(ShareData)} can't update content data {e}");
                }
                yield return waitTime;
            }
        }

        private IEnumerator ServerControlEvent()
        {
            WaitTime waitTime = new WaitTime(1000 * 2);
            while (true)
            {
                if (!Settings.Enable) ServerInstance.StopServer();
                else RunServer();
                yield return waitTime;
            }
        }
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
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(Client)} in Client -> {e}");
            Client.Close();
        }
    }
}

public class Server(int ServerPort)
{
    private TcpListener Listener = new TcpListener(IPAddress.Any, ServerPort);
    public bool IsRunning;

    public void StopServer()
    {
        if (!IsRunning) return;

        IsRunning = false;
        Listener.Stop();
    }

    public void StartServer()
    {
        if (IsRunning) return;

        IsRunning = true;
        Listener.Start();
    }
    public void RunServer()
    {
        if (IsRunning) return;

        try
        {
            StartServer();
            while (IsRunning)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback((tcpClient) => ClientThread(tcpClient)), Listener.AcceptTcpClient());
            }

            StopServer();
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(Server)} was crashed -> {e}");
            using (StreamWriter sw = new StreamWriter("ShareDataCrashLog"))
            {
                sw.Write($"{nameof(Server)} was crushed -> {e}");
            }
            StopServer();
        }
    }

    public void ClientThread(object? tcpClient)
    {
        if (tcpClient is null) return;
        try
        {
            new Client((TcpClient)tcpClient);
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(Server)} ClientThread was crushed -> {e}");
        }
    }
}