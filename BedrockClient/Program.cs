using System;

namespace BedrockClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Minecraft Bedrock Service Console");

            ConfigLoader.LoadConfigs();

            var helper = new ServerProcessHelper();
            helper.Run(args);
        }
    }
}
