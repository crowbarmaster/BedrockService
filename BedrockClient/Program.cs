using BedrockService;
using System;
using System.IO;
using System.Reflection;

namespace BedrockClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Minecraft Bedrock Service Console");

            var helper = new ServerProcessHelper(AppSettings.Instance.ServerConfig);
            helper.Run(args);
        }
    }
}
