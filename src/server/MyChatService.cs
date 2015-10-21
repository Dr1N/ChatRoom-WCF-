using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WcfChatService
{
    [ServiceContract]
    class MyChatService
    {
        private static List<string> users = new List<string>(100);
        private static List<Message> messages = new List<Message>(1000);
        private static Dictionary<string, DateTime> userTime = new Dictionary<string, DateTime>();
        private static ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
        private static int timeout = 10;

        [OperationContract]
        public bool Login(string userLogin)
        {
            try
            {
                bool tmpRes;
                locker.EnterReadLock();
                tmpRes = users.Contains(userLogin);
                locker.ExitReadLock();
                if (tmpRes == true)
                {
                    Console.WriteLine("Юзеру {0} отказано во входе", userLogin);
                    return false;
                }

                locker.EnterWriteLock();
                users.Add(userLogin);
                userTime[userLogin] = DateTime.Now;
                locker.ExitWriteLock();

                locker.EnterReadLock();
                Console.WriteLine("Юзер {0} залогинился в {1}", userLogin, userTime[userLogin]);
                locker.ExitReadLock();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                if (locker.IsReadLockHeld == true) { locker.ExitReadLock(); }
                if (locker.IsWriteLockHeld == true) { locker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public bool Logout(string userLogin)
        {
            try
            {
                bool tmpRes;
                locker.EnterReadLock();
                tmpRes = users.Contains(userLogin);
                locker.ExitReadLock();

                if (tmpRes == false)
                {
                    Console.WriteLine("Юзер {0} не разлогинен", userLogin);
                    return false;
                }
                locker.EnterWriteLock();
                users.Remove(userLogin);
                userTime.Remove(userLogin);
                locker.ExitWriteLock();
                Console.WriteLine("Юзер {0} разлогинился", userLogin);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                if (locker.IsReadLockHeld == true) { locker.ExitReadLock(); }
                if (locker.IsWriteLockHeld == true) { locker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public void SendMessage(string message, string user)
        {
            try
            {
                int tmpRes;
                locker.EnterReadLock();
                tmpRes = messages.Count;
                locker.ExitReadLock();

                if (tmpRes >= 1000)
                {
                    locker.ExitWriteLock();
                    messages.RemoveRange(0, 100);
                    locker.ExitWriteLock();
                }

                Message msg = new Message(DateTime.Now.ToShortTimeString(), user, message);

                locker.EnterWriteLock();
                messages.Add(msg);
                userTime[user] = DateTime.Now;
                locker.ExitWriteLock();

                locker.EnterReadLock();
                Console.WriteLine("Сообщение юзера {0} добавлено в {1}", user, userTime[user]);
                locker.ExitReadLock();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if (locker.IsReadLockHeld == true) { locker.ExitReadLock(); }
                if (locker.IsWriteLockHeld == true) { locker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public string[] GetUsers(string user)
        {
            string[] result = null;
            try
            {
                locker.EnterReadLock();
                result = users.ToArray();
                locker.ExitReadLock();

                locker.EnterWriteLock();
                userTime[user] = DateTime.Now;
                locker.ExitWriteLock();

                locker.EnterReadLock();
                Console.WriteLine("Отправили пользователей для {0} в {1}", user, userTime[user]);
                locker.ExitReadLock();

                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return result;
            }
            finally
            {
                if (locker.IsReadLockHeld == true) { locker.ExitReadLock(); }
                if (locker.IsWriteLockHeld == true) { locker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public string[] GetMessages(string user, ref int begin)
        {
            List<string> msgList = new List<string>();
            try
            {
                locker.EnterReadLock();
                int cnt = messages.Count;
                for (int i = begin; i < cnt; i++)
                {
                    msgList.Add(messages[i].ToString());
                }
                locker.ExitReadLock();

                locker.EnterWriteLock();
                userTime[user] = DateTime.Now;
                locker.ExitWriteLock();

                locker.EnterReadLock();
                Console.WriteLine("Отправили сообщения для {0} в {1}", user, userTime[user]);
                locker.ExitReadLock();

                begin = cnt;
                return msgList.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            finally
            {
                if (locker.IsReadLockHeld == true) { locker.ExitReadLock(); }
            }
        }

        public static void CheckUsers()
        {
            try
            {
                Console.WriteLine("Проверка юзеров");
                List<string> usersForRemove = new List<string>();

                locker.EnterWriteLock();
                foreach (KeyValuePair<string, DateTime> item in userTime)
                {
                    string tmpUser = item.Key;
                    TimeSpan ts = DateTime.Now - item.Value;
                    Console.WriteLine("Запрос от {0} был {1} секунд назад", tmpUser, ts.TotalSeconds);
                    if (ts > TimeSpan.FromSeconds(timeout))
                    {
                        Console.WriteLine("Пользователь {0} удалён по таймауту", tmpUser);
                        users.Remove(item.Key);
                        usersForRemove.Add(item.Key);
                    }
                }
                locker.ExitWriteLock();

                foreach (string item in usersForRemove)
                {
                    userTime.Remove(item);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if (locker.IsWriteLockHeld == true) { locker.ExitWriteLock(); }
            }
        }
    }
}