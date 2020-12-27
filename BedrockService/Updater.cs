using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BedrockService
{
    public static class Updater
    {
        public static bool VersionChanged = false;
        public static string[] FileList;

        public static async Task<bool> CheckUpdates()
        {
            Console.WriteLine("Checking MCS Version and fetching update if needed...");
            var client = new HttpClient();
            string content = "";
            client.DefaultRequestHeaders.Add("User-Agent", "C# console program");
            try
            {
                content = await client.GetStringAsync("https://www.minecraft.net/en-us/download/server/bedrock");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Updater resulted in error: {e.Message}");
                return false;
            }
            string pattern = @"(https://minecraft.azureedge.net/bin-win/bedrock-server-)(.*)(.zip)";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            Match m = regex.Match(content);
            string DownloadPath = m.Groups[0].Value;
            string Version = m.Groups[2].Value;
            client.Dispose();

            if (File.Exists($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\bedrock_ver.ini"))
            {
                string LocalVer = File.ReadAllText($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\bedrock_ver.ini");
                if (LocalVer != Version)
                {
                    Console.WriteLine($"New version detected! Now fetching from {DownloadPath}...");
                    VersionChanged = true;
                    FetchBuild(DownloadPath).Wait();
                    File.WriteAllText($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\bedrock_ver.ini", Version);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("Version ini file missing, fetching build to recreate...");
                FetchBuild(DownloadPath).Wait();
                File.WriteAllText($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\bedrock_ver.ini", Version);
                return true;
            }
            return false;
        }

        public static async Task FetchBuild(string path)
        {
            string ZipDir = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\MCSFiles\Update.zip";
            if (!Directory.Exists($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\MCSFiles"))
            {
                Directory.CreateDirectory($@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\MCSFiles");
            }
            if (File.Exists(ZipDir))
            {
                File.Delete(ZipDir);
            }
            if (ConfigLoader.Configs["Globals"]["AcceptedMojangLic"] == "false")
            {
                Console.WriteLine("------First time download detected------\n");
                Console.WriteLine("You will need to agree to the Minecraft End User License Agreement");
                Console.WriteLine("in order to continue. Visit https://account.mojang.com/terms");
                Console.WriteLine("to view terms. Type \"Yes\" and press enter to confirm that");
                Console.WriteLine("you agree to said terms.");
                Console.Write("Do you agree to the terms? ");
                Console.Out.Flush();
                if (Console.ReadLine() != "Yes")
                {
                    return;
                }
                ConfigLoader.Configs["Globals"]["AcceptedMojangLic"] = "true";
                ConfigLoader.SaveGlobals();
                Console.WriteLine("Now downloading latest build of Minecraft Bedrock Server. Please wait...");
            }
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, path))
                {
                    using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(), stream = new FileStream(ZipDir, FileMode.Create, FileAccess.Write, FileShare.None, 256000, true))
                    {
                        try
                        {
                            await contentStream.CopyToAsync(stream);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Download zip resulted in error: {e.Message}");
                        }
                        httpClient.Dispose();
                        request.Dispose();
                        contentStream.Dispose();
                    }
                }
            }
        }
    }
}
