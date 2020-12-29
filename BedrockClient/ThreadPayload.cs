namespace BedrockClient
{
    public class ThreadPayLoad
    {
        public delegate void ConsoleWriteLineDelegate(string value);

        public ConsoleWriteLineDelegate ConsoleWriteLine { get; set; }
        public int PortNumber { get; set; }
        public string IPAddr { get; set; }
        public string ShortName { get; set; }

        public ThreadPayLoad(ConsoleWriteLineDelegate consoleWriteLine, string addr, int portNumber, string name)
        {
            ConsoleWriteLine = consoleWriteLine;
            PortNumber = portNumber;
            IPAddr = addr;
            ShortName = name;
        }
    }
}
