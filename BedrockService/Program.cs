using log4net.Config;
using System;
using Topshelf;

namespace BedrockService
{
    class Program
    {
        public static bool DebugModeEnabled = false;
        static void Main(string[] args)
        {

            XmlConfigurator.Configure();

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
                    s.BeforeStartingService(_ => Console.WriteLine("Starting service..."));
                    s.BeforeStoppingService(_ => Console.WriteLine("Stopping service..."));

                });


                x.RunAsLocalSystem();
                x.SetDescription("Windows Service Wrapper for Windows Bedrock Server");
                x.SetDisplayName("BedrockService");
                x.SetServiceName("BedrockService");

                x.EnableServiceRecovery(src =>
                {
                    src.RestartService(delayInMinutes: 0);
                    src.RestartService(delayInMinutes: 1);
                    src.SetResetPeriod(days: 1);
                });

            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            if (DebugModeEnabled)
            {
                Console.Write("Program is force-quitting. Press any key to exit.");
                Console.Out.Flush();
                Console.ReadLine();
            }
            Environment.ExitCode = exitCode;
        }
    }
}
