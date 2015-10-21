using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WcfChatService
{
    [ServiceContract]
    interface MyChatService
    {
        [OperationContract]
        bool Login(string userLogin);

        [OperationContract]
        bool Logout(string userLogin);

        [OperationContract]
        void SendMessage(string message, string user);

        [OperationContract]
        string[] GetUsers(string user);

        [OperationContract]
        string[] GetMessages(string user, ref int begin);
    }
}