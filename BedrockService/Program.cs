using log4net.Config;
using System;
using Topshelf;

namespace BedrockService
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

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
