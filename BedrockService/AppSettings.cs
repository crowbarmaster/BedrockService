using System.Collections.Generic;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace BedrockService
{
    public class XmlConfigurationSection : ConfigurationSection
    {
        // This may be fetched multiple times: XmlReaders can't be reused, so load it into an XDocument instead
        private XDocument document;

        protected override void DeserializeSection(XmlReader reader)
        {
            document = XDocument.Load(reader);
        }

        protected override object GetRuntimeObject()
        {
            // This is cached by ConfigurationManager, so no point in duplicating it to stop other people from modifying it
            return document;
        }
    }

    public class AppSettings
    {
        private const string sectionName = "settings";
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(AppSettings), new XmlRootAttribute(sectionName));

        public static readonly AppSettings Instance;

        static AppSettings()
        {
            var document = (XDocument)ConfigurationManager.GetSection(sectionName);
            Instance = (AppSettings)serializer.Deserialize(document.CreateReader());
        }

        public List<ServerConfig> ServerConfig { get; set; }

    }

    public class ServerConfig
    {
        public string ServerName { get; set; }
        public string ShortName { get; set; }
        public string ServerPort4 { get; set; }
        public string ServerPort6 { get; set; }
        public string BedrockServerExeLocation { get; set; }
        public string BedrockServerExeName { get; set; }
        public string BedrockServerConfigFile { get; set; }
        public string AcceptedMojangLic { get; set; }
        public string BackupFolderName { get; set; }
        public string AdvancedBackup { get; set; }
        public string MaxBackupCount { get; set; }
        public bool Primary { get; set; }

        public Command StartupCommands { get; set; }

        public int WCFPortNumber { get; set; }
    }

    public class Command
    {
        public List<string> CommandText { get; set; }
    }
}
