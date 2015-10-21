using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WcfChatService
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = new ServiceHost(typeof(MyChatService));
            host.Open();
            Console.WriteLine("Сервер запущен. Для завершения нажмите любую кнопку.\n");

            Thread checkThr = new Thread(Check);
            checkThr.IsBackground = true;
            checkThr.Start();

            Console.ReadKey();
            host.Close();
        }

        static void Check()
        {
            while (true)
            {
                Thread.Sleep(2000);
                MyChatService.CheckUsers();
            }
        }
    }
}