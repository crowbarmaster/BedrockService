using System.ServiceModel;

namespace BedrockService
{
    [ServiceContract]
    public interface IWCFConsoleServer
    {
        /// <summary>
        /// Gets the latest Console messages
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        string GetConsole();

        /// <summary>
        /// Sends new commands
        /// </summary>
        /// <param name="command"></param>
        [OperationContract]
        void SendConsoleCommand(string command);

        [OperationContract]
        string GetVersion();
    }
}
