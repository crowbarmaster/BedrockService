using System;
using System.Diagnostics;
using System.Threading;

namespace BedrockClient
{
    /// <summary>
    /// Helper methods to run per process
    /// </summary>
    internal class ServerInfo
    {
        public string IPAddr { get; }
        public int Port { get; }
        public string ShortName { get; }

        public ServerInfo() { }

        public ServerInfo(string addr, int port, string name)
        {
            IPAddr = addr;
            Port = port;
            ShortName = name;
        }

        /// <summary>
        /// Start a new client window with the given Port
        /// </summary>
        public void StartProcess()
        {
            var process = new Process();
            var info = new ProcessStartInfo {FileName = "BedrockClient", Arguments = $"{IPAddr} {Port}"};

            process.StartInfo = info;
            process.Start();

            Console.WriteLine($"Opened new client window for port {IPAddr}:{Port}");
        }

        /// <summary>
        /// Connect to the process with the given port, and pass all arguments. Closes client after.
        /// </summary>
        /// <param name="args">Arguments to send to the server</param>
        public void SendCommands(Args args)
        {
            ConnectToServer();
            args.ExitParams.ForEach(p =>
            {
                SendCommand(p);
                Thread.Sleep(500);
            });

            Environment.Exit(1);
        }

        /// <summary>
        /// Connect to the process with the given port
        /// </summary>
        public void Connect()
        {
            ConnectToServer();

            while (true)
            {
                var command = Console.ReadLine();
                SendCommand(command);
            }
        }

        /// <summary>
        /// Opens a connection to the server. Required to send commands.
        /// </summary>
        private void ConnectToServer()
        {
            ClientConnector.Connect(Console.WriteLine, IPAddr, Port, ShortName);

            // start the connection with server to get output
            Thread outputThread = new Thread(ClientConnector.OutputThread) { Name = "ChildIO Output Console" };

            outputThread.Start(new ThreadPayLoad(Console.WriteLine, IPAddr, Port, ShortName));
        }

        /// <summary>
        /// Sends the specified command to the server
        /// </summary>
        /// <param name="command"></param>
        private static void SendCommand(string command)
        {
            ClientConnector.SendCommand(command, Console.WriteLine);
        }
    }
}