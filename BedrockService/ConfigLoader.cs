using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace BedrockService
{
    public static class ConfigLoader
    {
        public static Dictionary<string, Dictionary<string, string>> Configs;
        public static string ConfigDir = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\Configs"; // Get Executable directory for the root.
        public static string GlobalFile = $@"{ConfigDir}\Globals.conf";
        public static string DefaultFile = $@"{ConfigDir}\Default.conf";

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
            if (!Configs.ContainsKey("Globals"))
            {
                Console.WriteLine("Globals file missing! Regenerating default file...");
                CreateDefaultGlobals();
                LoadConfigs();
            }
            string pattern = @"^Service_(.*)$";
            Regex ServerRegex = new Regex(pattern);
            bool ServerCheck = false;
            foreach (string key in Configs.Keys)
            {
                if (ServerRegex.IsMatch(key))
                {
                    ServerCheck = true;
                }
            }
            if (!ServerCheck)
            {
                Console.WriteLine("Error: No server config files found or corrupt! Regenerating default file...");
                CreateDefaultConfigs();
                LoadConfigs();
            }
        }

        public static void SaveGlobals()
        {
            string[] Store = new string[Configs["Globals"].Count + 1];


            Store[0] = "[Globals]";
            List<string> Keys = Configs["Globals"].Keys.ToList();
            List<string> Vals = Configs["Globals"].Values.ToList();
            for (int i = 0; i < Configs["Globals"].Count; i++)
            {
                Store[i + 1] = $"{Keys[i]}={Vals[i]}";
            }

            File.WriteAllLines(GlobalFile, Store);
        }

        public static void CreateDefaultGlobals()
        {
            string[] Globals = new string[]
{
                "BackupEnabled=false",
                "BackupCron=0 1 * * *",
                "AcceptedMojangLic=false",
                "CheckUpdates=true",
                "UpdateCron=38 19 * * *"
};
            StringBuilder builder = new StringBuilder();
            builder.Append("[Globals]\n");
            foreach (string entry in Globals)
            {
                builder.Append($"{entry}\n");
            }
            File.WriteAllText(GlobalFile, builder.ToString());
        }

        public static void CreateDefaultConfigs()
        {
            string[] Service = new string[]
            {
                @"BedrockServerExeLocation=C:\Program Files (x86)\Minecraft Bedrock Server Launcher\Servers\Server\",
                @"BedrockServerExeName=bedrock_server.exe",
                @"WCFPortNumber=19134"
            };
            string[] Server = new string[]
            {
                "server-name=Test",
                "gamemode=creative",
                "difficulty=easy",
                "allow-cheats=false",
                "max-players=10",
                "online-mode=true",
                "white-list=true",
                "server-port=19132",
                "server-portv6=19133",
                "view-distance=32",
                "tick-distance=4",
                "player-idle-timeout=30",
                "max-threads=8",
                "level-name=Bedrock level",
                "level-seed=",
                "default-player-permission-level=member",
                "texturepack-required=false",
                "content-log-file-enabled=false",
                "compression-threshold=1",
                "server-authoritative-movement=server-auth",
                "player-movement-score-threshold=20",
                "player-movement-distance-threshold=0.3",
                "player-movement-duration-threshold-in-ms=500",
                "correct-player-movement=false"
            };
            string[] Whitelist = new string[]
            {
                "test=5555555555555555,false"
            };
            string[] Perms = new string[]
            {
                "visitor=5555555555555555"
            };
            string[] StartCmd = new string[]
            {
                "CmdDesc=help 1"
            };



            StringBuilder builder = new StringBuilder();
            builder.Append("[Service_Default]\n");
            foreach (string entry in Service)
            {
                builder.Append($"{entry}\n");
            }
            builder.Append("\n[Server_Default]\n");
            foreach (string entry in Server)
            {
                builder.Append($"{entry}\n");
            }
            builder.Append("\n[Perms_Default]\n");
            foreach (string entry in Perms)
            {
                builder.Append($"{entry}\n");
            }
            builder.Append("\n[Whitelist_Default]\n");
            foreach (string entry in Whitelist)
            {
                builder.Append($"{entry}\n");
            }
            builder.Append("\n[StartCmds_Default]\n");
            foreach (string entry in StartCmd)
            {
                builder.Append($"{entry}\n");
            }
            File.WriteAllText(DefaultFile, builder.ToString());
        }
    }
}
