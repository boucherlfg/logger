using System;
using Fleck;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace logger
{

    class Program
    {
        #region https://stackoverflow.com/questions/3571627/show-hide-the-console-window-of-a-c-sharp-console-application
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        #endregion

        static WRAP wrap;

        static void Main(string[] args)
        {
            wrap = new WRAP();
            wrap.StartServer();
            ShowWindow(GetConsoleWindow(), SW_HIDE);
            while (true) ;
        }
    }

    class WRAP
    {
        private WebSocketServer server;
        private DB db;

        public WRAP()
        {
            server = new WebSocketServer("ws://" + Ext.GetLocalIPAddress() + ":5001");
            db = new DB();
            db.Load("db.txt");
        }

        public void StartServer()
        {
            server.Start(socket =>
            {
                socket.OnMessage = message =>
                {
                    Console.WriteLine(DateTime.Now.ToString() + " " + socket.ConnectionInfo.ClientIpAddress + " : " + message);
                    socket.Send(Switch(message));
                };
            });
        }

        public string Switch(string line)
        {
            string sw = line.Split(' ').First();
            string arg = string.Join(" ", line.Split(' ').Skip(1));

            switch (sw)
            {
                case "add":
                    return Add(arg);
                case "search":
                    return Search(arg);
                default:
                    return "alert critical error : invalid command";
            }
        }
        private string Add(string line)
        {
            string usr = line.Split(' ').First();
            line = string.Join(" ", line.Split(' ').Skip(1));
            string pw = line.Split(' ').First();
            string msg = string.Join(" ", line.Split(' ').Skip(1));

            if (!string.IsNullOrEmpty(db.GetAll(usr, "")))
            {
                if (db.CheckCredentials(usr, pw))
                {
                    db.Add(usr, pw, msg);
                    db.Save("db.txt");
                    return "alert logged successfully";
                }
                else
                {
                    return "alert invalid username or password";
                }
            }
            else
            {
                db.Add(usr, pw, msg);
                db.Save("db.txt");
                return "alert created user and logged successfully";
            }

        }
        private string Search(string line)
        {
            string usr = line.Split(' ').First();
            line = string.Join(" ", line.Split(' ').Skip(1));
            string pw = line.Split(' ').First();
            string msg = string.Join(" ", line.Split(' ').Skip(1));

            if (db.CheckCredentials(usr, pw))
                return "alert " + db.GetAll(usr, msg);
            else return "alert invalid username or password";
        }
    }

    class DB
    {
        private List<LOG> logs;

        public bool CheckCredentials(string username, string password)
        {
            return logs.Exists(x => x.Username == username && x.Password == password);
        }
        public string GetAll(string username, string pattern)
        {
            string ret = "";
            logs.FindAll(x => x.Username == username 
                && x.Message.Contains(pattern)).ForEach(x =>
            {
                ret += x.ToLine() + "\n";
            });

            return ret.Trim()/*.Replace("\n", "<br>")*/;
        }
        public void Add(string username, string password, string message)
        {
            logs.Add(new LOG(username, password, message));
        }

        public void Save(string path)
        {
            File.WriteAllText(path, ToString(), Encoding.UTF8);
        }
        public void Load(string path)
        {
            if (!File.Exists("db.txt")) File.Create("db.txt").Close();
            logs = new List<LOG>();
            foreach (string log in File.ReadAllText(path, Encoding.UTF8).ExtractAll("log"))
            {
                logs.Add(LOG.Parse(log));
            }
        }

        public static DB Parse(string line)
        {
            DB ret = new DB();
            Array.ForEach(line.ExtractAll("log"), x => {
                ret.logs.Add(LOG.Parse(x));
            });
            return ret;
        }
        public override string ToString()
        {
            string ret = "";
            logs.ForEach(x =>
            {
                ret += x.ToString().PutInTag("log");
            });
            return ret;
        }
    }

    class LOG
    {
        private string username;
        private string password;
        private string time;
        private string message;

        public string Username
        {
            get
            {
                return username;
            }
        }
        public string Password
        {
            get
            {
                return password;
            }
        }
        public string Message
        {
            get
            {
                return message;
            }
        }
        public string Time
        {
            get
            {
                return time;
            }
        }

        public LOG() {}
        public LOG(string username, string password, string message)
        {
            time = DateTime.Now.ToString();
            this.username = username;
            this.password = password;
            this.message = message;
        }

        public static LOG Parse(string line)
        {
            return new LOG()
            {
                username = line.Extract("username"),
                password = line.Extract("password"),
                time = line.Extract("time"),
                message = line.Extract("message")
            };
        }
        public override string ToString()
        {
            return username.PutInTag("username") 
                + password.PutInTag("password") 
                + time.PutInTag("time") 
                + message.PutInTag("message");

        }
        public string ToLine()
        {
            return time + " : " + message;
        }
    }

    static class Ext
    {
        public static bool IsAlphaNumeric(this string line)
        {
            return line.All(x => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".Contains(x));
        }
        public static string PutInTag(this string line, string tag)
        {
            return "<" + tag + ">" + line + "</" + tag + ">";
        }
        public static string Extract(this string line, string tag)
        {
            return line.Split("</" + tag + ">").First().Split("<" + tag + ">").Last();
        }
        public static string[] ExtractAll(this string line, string tag)
        {
            string temp = line;
            List<string> ret = new List<string>();
            while(temp.Contains("</" + tag + ">"))
            {
                ret.Add(temp.Extract(tag));
                temp = temp.Substring(temp.IndexOf("</" + tag + ">") + ("</" + tag + ">").Length);
            }
            return ret.ToArray();
        }
        public static string[] Split(this string line, string sep)
        {
            string temp = line;
            List<string> ret = new List<string>();

            while (temp.Contains(sep))
            {
                ret.Add(temp.Substring(0, temp.IndexOf(sep)));
                temp = temp.Substring(temp.IndexOf(sep) + sep.Length);
            }
            ret.Add(temp);

            return ret.ToArray();
        }
        #region https://stackoverflow.com/questions/6803073/get-local-ip-address
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        #endregion 
    }
}
