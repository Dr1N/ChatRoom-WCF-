using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WcfChatService;

namespace MyChat
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Потоки

        private Thread usersThread;
        private Thread messagesThread;

        //Вспомогательные

        private string userLogin;
        private bool isCrahed;
        private DispatcherTimer timer;
        private MyChatService wcfClient;
        private ChannelFactory<MyChatService> CF;

        //Коллекции

        private List<Message> messages = new List<Message>(1000);
        private List<string> users = new List<string>(100);

        public MainWindow()
        {
            InitializeComponent();

            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromSeconds(2);
            this.timer.Tick += timer_Tick;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            this.lbUsers.ItemsSource = this.users;
            this.lbMessages.ItemsSource = this.messages;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (this.isCrahed == true)
            {
                this.Logout();
                this.Login();
            }
        }

        #region GUI_Events

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Login();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                lock (this.wcfClient)
                {
                    this.wcfClient.Logout(this.userLogin);
                }
            }
            catch { }
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            this.SendMessage();
            this.tbxMessage.Text = "";
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Logout();
        }

        #endregion

        #region Threads

        private void UpdateUsers()
        {
            Console.WriteLine("Поток обновления пользователей запущен");
            while (true)
            {
                Console.WriteLine("Запрос пользователей:");
                try
                {
                    string[] users;
                    lock (this.wcfClient)
                    {
                        users = this.wcfClient.GetUsers(this.userLogin);
                    }
                    if (users == null) 
                    {
                        Thread.Sleep(1000);
                        continue; 
                    }
                    this.users.Clear();
                    foreach (string item in users)
                    {
                        Console.WriteLine(item);
                        this.users.Add(item);
                    }
                    this.Dispatcher.Invoke(new Action(() => this.lbUsers.Items.Refresh()));
                    Thread.Sleep(2000);
                }
                catch(ThreadAbortException)
                {
                    Console.WriteLine("Поток обновления пользователей завершён");
                    break;
                }
                catch(Exception e)
                {
                    this.isCrahed = true;
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Поток обновления пользователей завершён");
                    break;
                }
            }
        }

        private void UpdateMessages()
        {
            Console.WriteLine("Поток обновления сообщений запущен");
            int begin = 0;
            while (true)
            {
                try
                {
                    Console.WriteLine("Запрос сообщений:");

                    if(this.messages.Count >= 1000) { this.messages.RemoveRange(0,100); }
                    
                    string[] sMessages;
                    lock (this)
                    {
                        sMessages = this.wcfClient.GetMessages(this.userLogin, ref begin);
                    }

                    if (sMessages == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    foreach (string item in sMessages)
                    {
                        Console.WriteLine(item);
                        string[] msg = item.Split('|');
                        this.messages.Add(new Message(msg[0], msg[1], msg[2]));
                    }
                    this.Dispatcher.Invoke(new Action(() => this.lbMessages.Items.Refresh()));
                    Thread.Sleep(1000);
                }
                catch(ThreadAbortException)
                {
                    Console.WriteLine("Поток обновления сообщений завершён");
                    break;
                }
                catch(Exception e)
                {
                    this.isCrahed = true;
                   Console.WriteLine(e.Message);
                   Console.WriteLine("Поток обновления сообщений завершён");
                   break;
                }
            }
        }

        #endregion

        #region Connection

        private void Login()
        {
            string login = "";
            while (true)
            {
                try
                {
                    this.CF = new ChannelFactory<MyChatService>("ENDPOINT");
                    this.wcfClient = this.CF.CreateChannel();
                    bool tmpResult;
                    if (this.GetUserLogin(ref login) == false)
                    {
                        Environment.Exit(0);
                    }
                    lock (this.wcfClient)
                    {
                        tmpResult = this.wcfClient.Login(login);
                    }
                    if (tmpResult == false)
                    {
                        MessageBox.Show("Не удалось присоединиться к чату", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        this.isCrahed = false;

                        this.userLogin = login;

                        this.usersThread = new Thread(this.UpdateUsers);
                        this.usersThread.IsBackground = true;
                        this.usersThread.Start();

                        this.messagesThread = new Thread(this.UpdateMessages);
                        this.messagesThread.IsBackground = true;
                        this.messagesThread.Start();

                        this.timer.Start();

                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void Logout()
        {
            try
            {
                bool tmpResult;
                lock (this.wcfClient)
                {
                    tmpResult = this.wcfClient.Logout(this.userLogin);
                }

                if (tmpResult == false)
                {
                    MessageBox.Show("При выходе из чата возникли проблемы", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                this.isCrahed = false;
                this.timer.Stop();
                this.usersThread.Abort();
                this.messagesThread.Abort();
                this.users.Clear();
                this.messages.Clear();
                this.lbUsers.Items.Refresh();
                this.lbMessages.Items.Refresh();
                this.tbxMessage.Text = "";

                this.Login();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void SendMessage()
        {
            try
            {
                string message = this.tbxMessage.Text.Trim().Replace('|', '☺');
                if (message.Length == 0) { return; }
                lock (this.wcfClient)
                {
                    this.wcfClient.SendMessage(message, this.userLogin);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Не удалось отправть сообщение", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Logout();
                this.Login();
                Console.WriteLine(e.Message);
            }
        }

        #endregion

        #region Helpers

        private bool GetUserLogin(ref string login)
        {
            Login loginWnd = new Login();
            loginWnd.Owner = this;
            bool? dialogResult = loginWnd.ShowDialog();
            if (dialogResult.HasValue && dialogResult.Value)
            {
                login = loginWnd.userLogin;
                return true;
            }
            return false;
        }
        
        #endregion
    }
}