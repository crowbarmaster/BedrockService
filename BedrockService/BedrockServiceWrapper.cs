using IniParser;
using IniParser.Model;
using IniParser.Parser;
using NCrontab;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using Topshelf;
using Topshelf.Logging;

namespace BedrockService
{
    public class BedrockServiceWrapper : ServiceControl
    {


        static List<BedrockServerWrapper> bedrockServers;


        static readonly LogWriter _log = HostLogger.Get<BedrockServiceWrapper>();

        HostControl _hostControl;

        const string serverProperties = "server.properties";
        const string serverName = "server-name";
        const string ipv4port = "server-port";
        const string ipv6port = "server-portv6";
        const string primaryipv4port = "19132";
        const string primaryipv6port = "19133";
        bool stopping;

        private System.Timers.Timer updaterTimer;
        private System.Timers.Timer cronTimer;
        CrontabSchedule shed;
        private CrontabSchedule updater;

        public BedrockServiceWrapper(bool throwOnStart, bool throwOnStop, bool throwUnhandled)
        {
            try
            {
                string pattern = @"^Service_(.*)$";
                Regex regex = new Regex(pattern);
                bedrockServers = new List<BedrockServerWrapper>();
                foreach (string Key in ConfigLoader.Configs.Keys)
                {
                    Match TestMatch = regex.Match(Key);
                    if (TestMatch.Success)
                    {
                        ServerConfig config = new ServerConfig();
                        foreach (KeyValuePair<string, string> kvp in ConfigLoader.Configs[TestMatch.Groups[0].Value])
                        {
                            if (kvp.Key.Equals("BedrockServerExeLocation"))
                            {
                                config.BedrockServerExeLocation = kvp.Value;
                            }
                            if (kvp.Key.Equals("WCFPortNumber"))
                            {
                                config.WCFPortNumber = Convert.ToInt32(kvp.Value);
                            }
                            if (kvp.Key.Equals("BedrockServerExeName"))
                            {
                                config.BedrockServerExeName = kvp.Value;
                            }
                            config.ShortName = TestMatch.Groups[1].Value;
                        }
                        bedrockServers.Add(new BedrockServerWrapper(config));
                        Console.WriteLine("Added config!");
                    }
                }

                shed = CrontabSchedule.TryParse(ConfigLoader.Configs["Globals"]["BackupCron"]);
                if (ConfigLoader.Configs["Globals"]["BackupEnabled"] == "true" && shed != null)
                {
                    cronTimer = new System.Timers.Timer((shed.GetNextOccurrence(DateTime.Now) - DateTime.Now).TotalMilliseconds);
                    cronTimer.Elapsed += CronTimer_Elapsed;
                    cronTimer.Start();
                }

                updater = CrontabSchedule.TryParse(ConfigLoader.Configs["Globals"]["UpdateCron"]);
                if (ConfigLoader.Configs["Globals"]["CheckUpdates"] == "true" && updater != null)
                {
                    updaterTimer = new System.Timers.Timer((updater.GetNextOccurrence(DateTime.Now) - DateTime.Now).TotalMilliseconds);
                    updaterTimer.Elapsed += UpdateTimer_Elapsed;
                    Console.WriteLine($"Updates Enabled, will be checked in: {((float)updaterTimer.Interval / 1000)} seconds.");
                    updaterTimer.Start();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"Error Instantiating BedrockServiceWrapper: {e.Message}");
            }

        }

        private void CronTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {

                cronTimer.Stop();
                cronTimer = null;
                if (ConfigLoader.Configs["Globals"]["BackupEnabled"] == "true" && shed != null)
                {
                    Backup();

                    cronTimer = new System.Timers.Timer((shed.GetNextOccurrence(DateTime.Now) - DateTime.Now).TotalMilliseconds);
                    cronTimer.Elapsed += CronTimer_Elapsed;
                    cronTimer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error in BackupTimer_Elapsed", ex);
            }
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                updaterTimer.Stop();
                updaterTimer = null;
                if (ConfigLoader.Configs["Globals"]["CheckUpdates"] == "true" && updater != null && Updater.CheckUpdates().Result)
                {
                    if (Updater.VersionChanged)
                    {
                        Console.WriteLine("Version change detected! Restarting server(s) to apply update...");
                        if (Stop(_hostControl))
                        {
                            Start(_hostControl);
                        }
                    }

                    updaterTimer = new System.Timers.Timer((updater.GetNextOccurrence(DateTime.Now) - DateTime.Now).TotalMilliseconds);
                    updaterTimer.Elapsed += UpdateTimer_Elapsed;
                    updaterTimer.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error in UpdateTimer_Elapsed", ex);
            }
        }

        private void Backup()
        {
            foreach (var brs in bedrockServers.OrderByDescending(t => t.ServerConfig.Primary).ToList())
            {
                brs.Stopping = true;
                if (!stopping) brs.StopControl();
                Thread.Sleep(1000);
            }

            foreach (var brs in bedrockServers.OrderByDescending(t => t.ServerConfig.Primary).ToList())
            {
                if (!stopping) brs.Backup();

            }
            foreach (var brs in bedrockServers.OrderByDescending(t => t.ServerConfig.Primary).ToList())
            {
                brs.Stopping = false;
                if (!stopping) brs.StartControl(_hostControl);
                Thread.Sleep(2000);

            }
        }

        public bool Stop(HostControl hostControl)
        {

            stopping = true;
            _hostControl = hostControl;
            try
            {
                foreach (var brs in bedrockServers)
                {
                    brs.Stopping = true;
                    brs.StopControl();
                    Thread.Sleep(1000);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error Stopping BedrockServiceWrapper {e.Message}");
                return false;
            }
        }



        public bool Start(HostControl hostControl)
        {

            _hostControl = hostControl;
            try
            {
                ValidSettingsCheck();

                foreach (var brs in bedrockServers.OrderByDescending(t => t.ServerConfig.Primary).ToList())
                {
                    _hostControl.RequestAdditionalTime(TimeSpan.FromSeconds(30));
                    brs.Stopping = false;
                    brs.StartControl(hostControl);
                    Thread.Sleep(2000);
                    Console.WriteLine($"AppName was: {brs.ServerConfig.BedrockServerExeName.Substring(0, brs.ServerConfig.BedrockServerExeName.Length - 4)}");
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error Starting BedrockServiceWrapper {e.Message}");
                return false;
            }
        }

        private void ValidSettingsCheck()
        {
            if (bedrockServers.Count() < 1)
            {
                throw new Exception("No Servers Configured");
            }
            else
            {
                var exeLocations = bedrockServers.GroupBy(t => t.ServerConfig.BedrockServerExeLocation + t.ServerConfig.BedrockServerExeName);
                if (exeLocations.Count() != bedrockServers.Count())
                {
                    throw new Exception("Duplicate Server Paths defined");
                }
                foreach (var server in bedrockServers)
                {
                    if (server.ServerConfig.BedrockServerExeName != "bedrock_server.exe" && File.Exists(server.ServerConfig.BedrockServerExeLocation + "bedrock_server.exe"))
                    {
                        if (File.Exists(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName))
                        {
                            File.Delete(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName);
                        }
                        File.Copy(server.ServerConfig.BedrockServerExeLocation + "bedrock_server.exe", server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName);
                        Console.WriteLine($@"Copied {server.ServerConfig.BedrockServerExeLocation + "bedrock_server.exe"} to {server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName}");
                    }
                    if (!File.Exists(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName))
                    {
                        if (File.Exists($@"{server.ServerConfig.BedrockServerExeLocation}FileList.ini"))
                        {
                            foreach (string file in File.ReadAllLines($@"{server.ServerConfig.BedrockServerExeLocation}FileList.ini"))
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch (Exception e)
                                {

                                }
                            }
                        }
                        try
                        {
                            ZipFile.ExtractToDirectory($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\MCSFiles\Update.zip", server.ServerConfig.BedrockServerExeLocation);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR: Got zipfile exception! {e.Message}");
                            DeleteFilesRecursively(new DirectoryInfo(server.ServerConfig.BedrockServerExeLocation));
                            ValidSettingsCheck();
                        }
                        string[] FileList = Directory.GetFiles($@"{server.ServerConfig.BedrockServerExeLocation.Substring(0, server.ServerConfig.BedrockServerExeLocation.Length - 1)}", "*", SearchOption.AllDirectories);
                        File.WriteAllLines($@"{server.ServerConfig.BedrockServerExeLocation}Filelist.ini", FileList);
                        ValidSettingsCheck();
                    }
                    else
                    {
                        if (Updater.VersionChanged)
                        {
                            Console.WriteLine($"Unpacking update for server in directory: {server.ServerConfig.BedrockServerExeLocation}. Please wait...");
                            foreach (string file in File.ReadAllLines($@"{server.ServerConfig.BedrockServerExeLocation}Filelist.ini"))
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch (Exception e)
                                {

                                }
                            }
                            try
                            {
                                ZipFile.ExtractToDirectory($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\MCSFiles\Update.zip", server.ServerConfig.BedrockServerExeLocation);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"ERROR: Got zipfile exception! {e.Message}");
                            }
                            if (server.ServerConfig.BedrockServerExeName != "bedrock_server.exe" && File.Exists(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName))
                            {
                                File.Delete(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName);
                                File.Copy(server.ServerConfig.BedrockServerExeLocation + "bedrock_server.exe", server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName);

                            }
                        }
                        FileInfo inf = new FileInfo(server.ServerConfig.BedrockServerExeLocation + server.ServerConfig.BedrockServerExeName);

                        string[] PropFile = new string[ConfigLoader.Configs[$"Server_{server.ServerConfig.ShortName}"].Count];
                        int count = 0;
                        foreach (KeyValuePair<string, string> kvp in ConfigLoader.Configs[$"Server_{server.ServerConfig.ShortName}"])
                        {
                            PropFile[count] = $"{kvp.Key}={kvp.Value}";
                            count++;
                        }
                        File.WriteAllLines($@"{server.ServerConfig.BedrockServerExeLocation}\server.properties", PropFile);
                        string[] JsonArrays = WriteJSONFiles(server.ServerConfig.ShortName);
                        File.WriteAllText($@"{server.ServerConfig.BedrockServerExeLocation}\whitelist.json", JsonArrays[0]);
                        File.WriteAllText($@"{server.ServerConfig.BedrockServerExeLocation}\permissions.json", JsonArrays[1]);

                        if (ConfigLoader.Configs[$"StartCmds_{server.ServerConfig.ShortName}"].Count > 0)
                        {
                            server.ServerConfig.StartupCommands = new Command();
                            server.ServerConfig.StartupCommands.CommandText = new List<string>();
                            foreach (KeyValuePair<string, string> cmd in ConfigLoader.Configs[$"StartCmds_{server.ServerConfig.ShortName}"])
                            {
                                server.ServerConfig.StartupCommands.CommandText.Add(cmd.Value);
                            }
                        }

                        FileInfo configfile = inf.Directory.GetFiles(serverProperties).ToList().Single();

                        IniDataParser parser = new IniDataParser();
                        parser.Configuration.AllowKeysWithoutSection = true;
                        parser.Configuration.CommentString = "#";

                        FileIniDataParser fp = new FileIniDataParser(parser);

                        IniData data = fp.ReadFile(configfile.FullName);

                        server.ServerConfig.ServerName = data.GetKey(serverName);
                        server.ServerConfig.ServerPort4 = data.GetKey(ipv4port);
                        server.ServerConfig.ServerPort6 = data.GetKey(ipv6port);

                    }
                }
                if (Updater.VersionChanged)
                {
                    Updater.VersionChanged = false;
                    ValidSettingsCheck();
                }
                var duplicateV4 = bedrockServers.GroupBy(x => x.ServerConfig.ServerPort4)
                    .Where(g => g.Count() > 1)
                    .Select(y => new ServerConfig() { ServerPort4 = y.Key })
                    .ToList();
                var duplicateV4Servers = bedrockServers.Where(t => duplicateV4.Select(r => r.ServerPort4).Contains(t.ServerConfig.ServerPort4)).ToList();
                if (duplicateV4Servers.Count() > 0)
                {
                    throw new Exception("Duplicate server IPv4 ports detected for: " + string.Join(", ", duplicateV4Servers.Select(t => t.ServerConfig.BedrockServerExeLocation)));
                }
                var duplicateV6 = bedrockServers.GroupBy(x => x.ServerConfig.ServerPort6)
                    .Where(g => g.Count() > 1)
                    .Select(y => new ServerConfig() { ServerPort6 = y.Key })
                    .ToList();
                var duplicateV6Servers = bedrockServers.Where(t => duplicateV6.Select(r => r.ServerPort6).Contains(t.ServerConfig.ServerPort6)).ToList();
                if (duplicateV6Servers.Count() > 0)
                {
                    throw new Exception("Duplicate server IPv6 ports detected for: " + string.Join(", ", duplicateV6Servers.Select(t => t.ServerConfig.BedrockServerExeLocation)));
                }
                var duplicateName = bedrockServers.GroupBy(x => x.ServerConfig.ServerName)
                    .Where(g => g.Count() > 1)
                    .Select(y => new ServerConfig() { ServerName = y.Key })
                    .ToList();
                var duplicateNameServers = bedrockServers.Where(t => duplicateName.Select(r => r.ServerName).Contains(t.ServerConfig.ServerName)).ToList();
                if (duplicateNameServers.Count() > 0)
                {
                    throw new Exception("Duplicate server names detected for: " + string.Join(", ", duplicateV6Servers.Select(t => t.ServerConfig.BedrockServerExeLocation)));
                }
                if (bedrockServers.Count > 1)
                {
                    if (!bedrockServers.Exists(t => t.ServerConfig.ServerPort4 == primaryipv4port && t.ServerConfig.ServerPort6 == primaryipv6port))
                    {
                        throw new Exception("No server defined with default ports " + primaryipv4port + " and " + primaryipv6port);
                    }
                    bedrockServers.Single(t => t.ServerConfig.ServerPort4 == primaryipv4port && t.ServerConfig.ServerPort6 == primaryipv6port).ServerConfig.Primary = true;
                }
                else
                {
                    bedrockServers.ForEach(t => t.ServerConfig.Primary = true);
                }
                var duplicateWCFPort = bedrockServers.GroupBy(x => x.ServerConfig.WCFPortNumber)
                    .Where(g => g.Count() > 1)
                    .Select(y => new ServerConfig() { WCFPortNumber = y.Key })
                    .ToList();
                if (duplicateWCFPort.Count > 1)
                {
                    throw new Exception("Duplicate WCFPortNumber detected as: " + string.Join(", ", duplicateWCFPort.Select(t => t.WCFPortNumber.ToString())));
                }
                var v4ports = bedrockServers.Select(t => t.ServerConfig.ServerPort4);
                var WCFports = bedrockServers.Select(t => t.ServerConfig.WCFPortNumber.ToString());
                var intersect = v4ports.Intersect(WCFports);
                if (intersect.Count() > 0)
                {
                    throw new Exception("Conflict exists between ports defined for the WCF Server and the Bedrock Server.  Conflicting port(s):" + string.Join(", ", intersect));
                }
            }

        }

        private static void DeleteFilesRecursively(DirectoryInfo source)
        {
            try
            {
                source.Delete(true);
            }
            catch(Exception e)
            {
                Console.WriteLine($@"Error Deleting Dir: {e.Message}");
            }
        }

        public string[] WriteJSONFiles(string ShortName)
        {
            string[] output = new string[2];
            StringBuilder sb = new StringBuilder();
            sb.Append("[\n");
            foreach (KeyValuePair<string, string> kvp in ConfigLoader.Configs[$"Whitelist_{ShortName}"])
            {
                sb.Append("\t{\n");
                sb.Append($"\t\t\"username\": \"{kvp.Key}\",\n");
                sb.Append($"\t\t\"xuid\": \"{kvp.Value.Split(',')[0]}\",\n");
                sb.Append($"\t\t\"ignoresPlayerLimit\": {kvp.Value.Split(',')[1]}\n");
                sb.Append("\t},\n");
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append("\n]");
            Console.WriteLine($"JSON Output was: {sb}");
            output[0] = sb.ToString();
            sb = new StringBuilder();
            sb.Append("[\n");
            foreach (KeyValuePair<string, string> kvp in ConfigLoader.Configs[$"Perms_{ShortName}"])
            {
                sb.Append("\t{\n");
                sb.Append($"\t\t\"permission\": \"{kvp.Key}\",\n");
                sb.Append($"\t\t\"xuid\": \"{kvp.Value}\"\n");
                sb.Append("\t},\n");
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append("\n]");
            Console.WriteLine($"JSON Output was: {sb}");
            output[1] = sb.ToString();
            return output;
        }
    }
}
