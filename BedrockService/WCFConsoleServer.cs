using System;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;

namespace BedrockService
{
    public class WCFConsoleServer : IWCFConsoleServer
    {
        public delegate string CurrentConsole();

        static Process _process;

        /// <summary>
        /// holds a call to get the console buffer
        /// </summary>
        static CurrentConsole _currentConsole;

        ServiceHost _serviceHost;

        public WCFConsoleServer()
        {
        }

        public WCFConsoleServer(Process process, CurrentConsole currentConsole, int portNumber)
        {
            _process = process;
            _currentConsole = currentConsole;

            var binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.None;

            var baseAddress = new Uri($"net.tcp://localhost:{portNumber}/MinecraftConsole");

            _serviceHost = new ServiceHost(typeof(WCFConsoleServer), baseAddress);
            

            _serviceHost.AddServiceEndpoint(typeof(IWCFConsoleServer), binding, baseAddress);
            _serviceHost.Open();

        }
        public string GetConsole()
        {
            return _currentConsole();
        }

        public void SendConsoleCommand(string command)
        {
            _process.StandardInput.WriteLine(command);
        }

        public string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public void Close()
        {
            _serviceHost.Close();
        }

        public void Abort()
        {
            _serviceHost.Abort();
            _serviceHost = null;
        }
    }
}
