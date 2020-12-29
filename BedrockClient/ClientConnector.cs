using System;
using System.ServiceModel;
using System.Threading;
using static BedrockClient.ThreadPayLoad;

namespace BedrockClient
{
    public class ClientConnector
    {
        private static IWCFConsoleServer _server;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="consoleWriteLine"></param>
        /// <param name="portNumber">default is 19134</param>
        public static void Connect(ConsoleWriteLineDelegate consoleWriteLine, string addr, int portNumber, string name)
        {
            var binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.None;

            var url = $"net.tcp://{addr}:{portNumber}/MinecraftConsole";
            var address = new EndpointAddress(url);
            var channelFactory =
                new ChannelFactory<IWCFConsoleServer>(binding, address);

            do
            {
                _server = channelFactory.CreateChannel();
                if (_server == null)
                {
                    Console.WriteLine($"Trying to connect to {url} on server {name}");
                }
                else
                {
                    try
                    {
                        _server.GetVersion();
                        consoleWriteLine($"Connection to '{url}' established on server {name}.");
                    }
                    catch(EndpointNotFoundException)
                    {
                        consoleWriteLine($"Trying to connect to {url} on server {name}");
                        _server = null;
                    }
                }
            }
            while (_server == null);
        }

        public static void OutputThread(object threadPayloadObject)
        {
            var threadPayload = (ThreadPayLoad)threadPayloadObject;

            while (true)
            {
                try
                {
                    var consoleOutput = _server.GetConsole();

                    if (string.IsNullOrWhiteSpace(consoleOutput))
                    {
                        Thread.Sleep(250);
                    }
                    else
                    {
                        threadPayload.ConsoleWriteLine(consoleOutput);
                    }
                }
                catch(CommunicationException)
                {
                    // start connection attempts again
                    threadPayload.ConsoleWriteLine("Lost connection to server.");
                    Connect(threadPayload.ConsoleWriteLine, threadPayload.IPAddr, threadPayload.PortNumber, threadPayload.ShortName);
                }
            }
        }

        public static void SendCommand(string command, ConsoleWriteLineDelegate consoleWriteLine)
        {
            try
            {
                _server.SendConsoleCommand(command);
            }
            catch(CommunicationObjectFaultedException)
            {
                consoleWriteLine($"ERROR:Connection to server lost command '{command}' was not processed. Please try again.");
            }
        }
    }
}
