using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BedrockClient
{
    class ConfigLoader
    {
        public static Dictionary<string, Dictionary<string, string>> Configs;
        public static string ConfigDir = $@"{Directory.GetCurrentDirectory()}\Configs";
        public static string ConfigFile = $@"{ConfigDir}\Config.conf";
        public static List<ServerInfo> ServerInfo;

        public static void LoadConfigs()
        {
            Configs = new Dictionary<string, Dictionary<string, string>>();
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }
            string[] files = Directory.GetFiles(ConfigDir, "*.conf");
            string SubPattern = @"^\[(\w*)\]$";
            string ActiveConfig = "";
            Regex regex = new Regex(SubPattern);

            if (files.Length > 0)
            {
                foreach (string file in files)
                {
                    string[] lines = File.ReadAllLines(file);
                    foreach (string line in lines)
                    {
                        if (regex.IsMatch(line))
                        {
                            Configs.Add(regex.Match(line).Groups[1].Value, new Dictionary<string, string>());
                            ActiveConfig = regex.Match(line).Groups[1].Value;
                        }
                        else if (line == "" || line == null || line.StartsWith("#"))
                        {
                            //Do nothing.
                        }
                        else
                        {
                            string[] split = line.Split('=');
                            if (split.Length == 1)
                            {
                                split[1] = "";
                            }
                            Configs[ActiveConfig].Add(split[0], split[1]);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Config file missing! Regenerating default file...");
                CreateDefaultConfig();
                LoadConfigs();
            }

            try
            {
                string pattern = @"^Config_(.*)$";
                Regex regx = new Regex(pattern);
                ServerInfo = new List<ServerInfo>();
                string addr = "";
                string name = "";
                int[] ports = null;

                foreach (string Key in Configs.Keys)
                {
                    Match TestMatch = regx.Match(Key);
                    if (TestMatch.Success)
                    {
                        foreach (KeyValuePair<string, string> kvp in Configs[TestMatch.Groups[0].Value])
                        {
                            if (kvp.Key.Equals("address"))
                            {
                                addr = kvp.Value;
                            }
                            if (kvp.Key.Equals("ports"))
                            {
                                ports = GetPorts(kvp.Value);
                            }
                            name = TestMatch.Groups[1].Value;
                        }
                        foreach (int port in ports)
                        {
                            ServerInfo.Add(new ServerInfo(addr, port, name));
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        public static void CreateDefaultConfig()
        {
            string[] Config = new string[]
{
                "address=localhost",
                "ports=19134"
};
            StringBuilder builder = new StringBuilder();
            builder.Append("[Config_Default]\n");
            foreach (string entry in Config)
            {
                builder.Append($"{entry}\n");
            }
            File.WriteAllText(ConfigFile, builder.ToString());
        }

        public static int[] GetPorts(string input)
        {
            string[] StrArr = input.Split(';');
            int[] Output = new int[StrArr.Length];

            for (int i = 0; i < StrArr.Length; i++)
            {
                Output[i] = Convert.ToInt32(StrArr[i]);
            }
            return Output;
        }
    }
}
