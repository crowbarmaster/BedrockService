﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace BedrockClient
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
