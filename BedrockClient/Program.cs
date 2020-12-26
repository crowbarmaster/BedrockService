using BedrockService;
using System;
using System.IO;

namespace BedrockClient
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
            }
        }

        public static void AppendToLog(string TextToLog)
        {
            if (TextToLog != null)
            {
                string output = $"{TextToLog}\n";
                File.AppendAllText(LogPath, output);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Minecraft Bedrock Service Console");

            var helper = new ServerProcessHelper(AppSettings.Instance.ServerConfig);
            helper.Run(args);
        }
    }
}
