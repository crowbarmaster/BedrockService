using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Topshelf;
using Topshelf.Logging;

namespace BedrockService
{
    public class BedrockServerWrapper
    {
        Process process;
        static readonly LogWriter _log = HostLogger.Get<BedrockServerWrapper>();
        Thread outputThread;
        Thread errorThread;
        Thread inputThread;
        Thread WCFServerThread;
        Thread watchDogThread;
        WCFConsoleServer wcfConsoleServer;
        string loggedThroughput;
        StringBuilder consoleBufferServiceOutput = new StringBuilder();
        bool serverStarted = false;
        int RestartLimit = 3;
        int RestartCount = 0;

        const string worldsFolder = "worlds";
        const string startupMessage = "[INFO] Server started.";
        HostControl hostController;
        public BedrockServerWrapper(ServerConfig serverConfig)
        {

            ServerConfig = serverConfig;

        }
        public BackgroundWorker Worker { get; set; }
        public ServerConfig ServerConfig { get; set; }

        public bool BackingUp { get; set; }
        public bool Stopping { get; set; }



        public void StopControl()
        {
            if (!(process is null))
            {
                _log.Info("Sending Stop to Bedrock . Process.HasExited = " + process.HasExited.ToString());

                process.StandardInput.WriteLine("stop");
                while (!process.HasExited) { }

                //_log.Info("Sent Stop to Bedrock . Process.HasExited = " + process.HasExited.ToString());
            }
            if (!(Worker is null))
            {
                Worker.CancelAsync();
                while (Worker.IsBusy) { Thread.Sleep(10); }
                Worker.Dispose();
            }
            Worker = null;
            process = null;
            GC.Collect();
            serverStarted = false;
            loggedThroughput = null;

        }

        public void StartControl(HostControl hostControl)
        {
            if (Worker is null)
            {
                Worker = new BackgroundWorker() { WorkerSupportsCancellation = true };
            }
            //while (BackingUp)
            //{
            //    Thread.Sleep(100);
            //}
            if (!Worker.IsBusy)
            {
                Worker.DoWork += (s, e) =>
                {
                    RunServer(hostControl);
                };
                Worker.RunWorkerAsync();
            }
        }

        public void RunServer(HostControl hostControl)
        {
                hostController = hostControl;
            try
            {
                Console.WriteLine($@"App {ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4)} running?");
                if (MonitoredAppExists(ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4)))
                {
                    Process[] processList = Process.GetProcessesByName(ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4));
                    if (processList.Length != 0)
                    {
                        Console.WriteLine($@"App {ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4)} running!");

                        foreach (Process process in processList)
                        {
                            try
                            {
                                process.Kill();
                                Thread.Sleep(1000);
                                Console.WriteLine($@"App {ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4)} killed?");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Killing proccess resulted in error: {e.Message}");
                            }
                        }
                    }
                }

                if (File.Exists(ServerConfig.BedrockServerExeLocation + ServerConfig.BedrockServerExeName))
                {
                    // Fires up a new process to run inside this one
                    process = Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = ServerConfig.BedrockServerExeLocation + ServerConfig.BedrockServerExeName
                    });
                    process.PriorityClass = ProcessPriorityClass.RealTime;

                    // Depending on your application you may either prioritize the IO or the exact opposite
                    const ThreadPriority ioPriority = ThreadPriority.Highest;
                    if (inputThread != null) inputThread.Interrupt();
                    if (errorThread != null) errorThread.Interrupt();
                    if (outputThread != null) outputThread.Interrupt();


                    outputThread = new Thread(outputReader) { Name = "ChildIO Output", Priority = ioPriority };
                    errorThread = new Thread(errorReader) { Name = "ChildIO Error", Priority = ioPriority };
                    inputThread = new Thread(inputReader) { Name = "ChildIO Input", Priority = ioPriority };

                    //Set as background threads (will automatically stop when application ends)
                    outputThread.IsBackground = errorThread.IsBackground
                        = inputThread.IsBackground = true;

                    //Start the IO threads
                    outputThread.Start(process);
                    errorThread.Start(process);
                    inputThread.Start(process);

                    WCFServerThread = new Thread(new ThreadStart(WCFThread));
                    WCFServerThread.Start();

                    if (watchDogThread == null || !watchDogThread.IsAlive)
                    {
                        try
                        {
                            watchDogThread = new Thread(new ThreadStart(Monitor));
                            watchDogThread.Start();
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
                else
                {
                    _log.Error("The Bedrock Server is not accessible at " + ServerConfig.BedrockServerExeLocation + ServerConfig.BedrockServerExeName + "\r\nCheck if the file is at that location and that permissions are correct.");
                    hostControl.Stop();
                }
            }
            catch (Exception e)
            {
                _log.Fatal("Error Running Bedrock Server", e);
                hostControl.Stop();

            }

        }

        private void WCFThread()
        {
            try
            {
                wcfConsoleServer = new WCFConsoleServer(process, GetCurrentConsole, ServerConfig.WCFPortNumber);
                _log.Debug("Before process.WaitForExit()");
                process.WaitForExit();
                _log.Debug("After process.WaitForExit()");

                process = null;

                _log.Debug("Stop WCF service");
                wcfConsoleServer.Close();
                GC.Collect();
            }
            catch (ThreadAbortException abort)
            {
                Console.WriteLine($"WCF Thread reports {abort.Message}");
            }
        }

        private bool MonitoredAppExists(string monitoredAppName)
        {
            try
            {
                Process[] processList = Process.GetProcessesByName(monitoredAppName);
                if (processList.Length == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ApplicationWatcher MonitoredAppExists Exception: " + ex.StackTrace);
                return true;
            }
        }

        public void Monitor()
        {
            if (!MonitoredAppExists(ServerConfig.BedrockServerExeName.Substring(0, ServerConfig.BedrockServerExeName.Length - 4)) && !Stopping)
            {
                StopControl();
                if (wcfConsoleServer != null)
                {
                    wcfConsoleServer.Abort();
                    WCFServerThread.Abort();
                    WCFServerThread = null;
                }
                StartControl(hostController);
            }
            else
            {
                Thread.Sleep(5000);
                if (!Stopping || !BackingUp)
                {
                    if(RestartCount < RestartLimit)
                    {
                        Monitor();
                    }
                    else
                    {
                        StopControl();
                        Environment.Exit(1);
                    }
                }
            }
        }

        /// <summary>
        /// Continuously copies data from one stream to the other.
        /// </summary>
        /// <param name="instream">The input stream.</param>
        /// <param name="outstream">The output stream.</param>
        private void passThrough(Stream instream, Stream outstream, string source)
        {
            try
            {
                byte[] buffer = new byte[4096];
                _log.Debug($"Starting passThrough for [{source}]");
                while (true)
                {
                    int len;
                    while ((len = instream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outstream.Write(buffer, 0, len);
                        outstream.Flush();
                        consoleBufferServiceOutput.Append(Encoding.ASCII.GetString(buffer).Substring(0, len).Trim());

                        if (consoleBufferServiceOutput.Length > 10000000)
                        {
                            consoleBufferServiceOutput = new StringBuilder(consoleBufferServiceOutput.ToString().Substring(consoleBufferServiceOutput.Length - 11000000));
                        }
                        _log.Debug(Encoding.ASCII.GetString(buffer).Substring(0, len).Trim());

                        if (!serverStarted)
                        {
                            loggedThroughput += Encoding.ASCII.GetString(buffer).Substring(0, len).Trim();
                            if (loggedThroughput.Contains(startupMessage))
                            {
                                serverStarted = true;

                                if (ServerConfig.StartupCommands != null)
                                {
                                    RunStartupCommands();
                                }

                            }
                        }
                    }
                    Thread.Sleep(100);

                }
            }
            catch (ThreadInterruptedException e)
            {
                _log.Debug($"Interrupting thread from [{source}]", e);
            }
            catch (ThreadAbortException e)
            {
                _log.Info($"Aborting thread from [{source}]", e);
            }
            catch (Exception e)
            {
                _log.Fatal($"Error Sending Stream from [{source}]", e);

            }
        }

        private void outputReader(object p)
        {
            var process = (Process)p;
            // Pass the standard output of the child to our standard output
            passThrough(process.StandardOutput.BaseStream, Console.OpenStandardOutput(), "OUTPUT");
        }

        private void errorReader(object p)
        {
            var process = (Process)p;
            // Pass the standard error of the child to our standard error
            passThrough(process.StandardError.BaseStream, Console.OpenStandardError(), "ERROR");
        }

        private void inputReader(object p)
        {
            var process = (Process)p;
            // Pass our standard input into the standard input of the child
            passThrough(Console.OpenStandardInput(), process.StandardInput.BaseStream, "INPUT");
        }

        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public void Backup()
        {
            try
            {

                BackingUp = true;
                FileInfo exe = new FileInfo(ServerConfig.BedrockServerExeLocation + ServerConfig.BedrockServerExeName);

                if (ServerConfig.BackupFolderName.Length > 0)
                {
                    DirectoryInfo serverDir = new DirectoryInfo(ServerConfig.BedrockServerExeLocation.Substring(0, ServerConfig.BedrockServerExeLocation.Length - 1));
                    DirectoryInfo worldsDir = new DirectoryInfo($"{ServerConfig.BedrockServerExeLocation}worlds");
                    DirectoryInfo backupDir = new DirectoryInfo($@"{ServerConfig.BackupFolderName}\{ServerConfig.ShortName}");
                    if (!Directory.Exists(backupDir.FullName))
                    {
                        Directory.CreateDirectory($@"{ServerConfig.BackupFolderName}\{ServerConfig.ShortName}");
                    }
                    int dirCount = backupDir.GetDirectories().Length; // this line creates a new int with a value derived from the number of directories found in the backups folder.
                    try // use a try catch any time you know an error could occur.
                    {
                        if(dirCount >= Convert.ToInt32(ServerConfig.MaxBackupCount)) // Compare the directory count with the value set in the config. Values from config are stored as strings, and therfore must be converted to integer first for compare.
                        {
                            string pattern = $@"Backup_(.*)$"; // This is a regular expression pattern. If you would like to know more, Grab notepad++ and play with regex search, a lot of guides out there.
                            Regex reg = new Regex(pattern); // Creates a new Regex class with our pattern loaded.
                            
                            List<long> Dates = new List<long>(); // creates a new list integer array named Dates, and initializes it.
                            foreach(DirectoryInfo dir in backupDir.GetDirectories()) // Loop through the array of directories in backup folder. In this "foreach" loop, we name each entry in the array "dir" and then do something to it.
                            {
                                if (reg.IsMatch(dir.Name)) // Using regex.IsMatch will return true if the pattern matches the name of the folder we are working with. 
                                {
                                    Match match = reg.Match(dir.Name); // creates an instance of the match to work with.
                                    Dates.Add(Convert.ToInt64(match.Groups[1].Value)); // if it was a match, we then pull the number we saved in the (.*) part of the pattern from the groups method in the match. Groups saves the entire match first, followed by anthing saved in parentheses. Because we need to compare dates, we must convert the string to an integer.
                                }
                            }
                            long OldestDate = 0; // Create a new int to store the oldest date in.
                            foreach (long date in Dates) // for each date in the Dates array....
                            {
                                if (OldestDate == 0) // if this is the first entry in Dates, OldestDate will still be 0. Set it to a date so compare can happen.
                                {
                                    OldestDate = date; // OldestDate now equals date.
                                }
                                else if (date < OldestDate) // If now the next entry in Dates is a smaller number than the previously set OldestDate, reset OldestDate to date.
                                {
                                    OldestDate = date; // OldestDate now equals date.
                                }
                            }
                            Directory.Delete($@"{backupDir}\Backup_{OldestDate}", true); // After running through all directories, this string $@"{backupDir}\Backup_{OldestDate}" should now represent the folder that has the lowest/oldest date. Delete it. Supply the "true" after the directory string to enable recusive mode, removing all files and folders.
                        }
                    }
                    catch (Exception e) // catch all exceptions here.
                    {
                        if (e.GetType() == typeof(FormatException)) // if the exception is equal a type of FormatException, Do the following... if this was a IOException, they would not match.
                        {
                            Console.WriteLine("Error in Config! MaxBackupCount must be nothing but a number!"); // this exception will be thrown if the string could not become a number (i.e. of there was a letter in the mix).
                        }
                    }
                    
                    var targetDirectory = backupDir.CreateSubdirectory($"Backup_{DateTime.Now.ToString("yyyyMMddhhmmss")}");
                    Console.WriteLine($"Backing up files for server {ServerConfig.ShortName}. Please wait!");
                    if(ServerConfig.AdvancedBackup == "false")
                    {
                        CopyFilesRecursively(worldsDir, targetDirectory);
                    }
                    else if (ServerConfig.AdvancedBackup == "true")
                    {
                         CopyFilesRecursively(serverDir, targetDirectory);
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error($"Error with Backup", e);
            }
            finally
            {
                BackingUp = false;
            }
        }

        private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            _log.Info("Starting Backup");
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                if (file.Name != "bedrock_server.pdb")
                {
                    file.CopyTo(Path.Combine(target.FullName, file.Name));
                }
            _log.Info("Finished Backup");
        }

        private static void DeleteFilesRecursively(DirectoryInfo source)
        {
            _log.Info("Starting Backup");
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                DeleteFilesRecursively(dir);
            }
                
            foreach (FileInfo file in source.GetFiles()) 
            { 
                 file.Delete();
                 Directory.Delete(source.FullName);
            }
               
            _log.Info("Finished Backup");
        }

        private void RunStartupCommands()
        {
            foreach (string s in ServerConfig.StartupCommands.CommandText)
            {
                process.StandardInput.WriteLine(s.Trim());
                Thread.Sleep(1000);
            }
        }

        public string GetCurrentConsole()
        {
            var sendConsole = consoleBufferServiceOutput.ToString();

            // clear out the buffer
            consoleBufferServiceOutput = new StringBuilder();

            return sendConsole;
        }
    }
}
