using log4net.Config;
using System;
using System.IO;
using System.Text;
using System.Threading;
using Topshelf;

namespace BedrockService
{
    class Program
    {
        public static Random rand = new Random();
        public static string LogPath = $@"{Directory.GetCurrentDirectory()}\Logs\Logfile.log";

        public static void LogSetup()
        {
            string LogPathRand = $@"{Directory.GetCurrentDirectory()}\Logs\Logfile_{rand.Next(111111, 999999)}.log";
            if (File.Exists(LogPath))
            {
                File.Copy(LogPath, LogPathRand);
                Thread.Sleep(100);
            }
            if (!Directory.Exists($@"{Directory.GetCurrentDirectory()}\Logs"))
            {
                Directory.CreateDirectory($@"{Directory.GetCurrentDirectory()}\Logs");
            }
            if (!File.Exists(LogPath))
            {
                File.Create(LogPath);
                Thread.Sleep(100);
            }
        }

    public static void AppendToLog (string TextToLog)
        {
            if(TextToLog != null)
            {
                string output = $"{TextToLog}\n";
                File.AppendAllText(LogPath, output);
                Thread.Sleep(100);
            }
        }

        static void Main(string[] args)
        {

            XmlConfigurator.Configure();
            LogSetup();
            ConfigLoader.LoadConfigs();
            Updater.CheckUpdates().Wait();

            var rc = HostFactory.Run(x =>
            {
                x.SetStartTimeout(TimeSpan.FromSeconds(10));
                x.SetStopTimeout(TimeSpan.FromSeconds(10));
                x.UseLog4Net();
                x.UseAssemblyInfoForServiceInfo();
                bool throwOnStart = false;
                bool throwOnStop = false;
                bool throwUnhandled = false;
                x.Service(settings => new BedrockServiceWrapper(throwOnStart, throwOnStop, throwUnhandled), s =>
                {
                    s.BeforeStartingService(_ => Console.WriteLine("BeforeStart"));
                    s.BeforeStoppingService(_ => Console.WriteLine("BeforeStop"));

                });


                x.RunAsLocalSystem();
                x.SetDescription("Windows Service Wrapper for Windows Bedrock Server");
                x.SetDisplayName("BedrockService");
                x.SetServiceName("BedrockService");

            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Console.Write("Program is force-quitting. Press any key to exit.");
            Console.Out.Flush();
            Console.ReadLine();
            Environment.ExitCode = exitCode;
        }
    }
}
