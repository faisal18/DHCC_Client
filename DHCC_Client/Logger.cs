using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DHCC_Client
{
    public class Logger
    {

        private static string baseDir = ConfigurationManager.AppSettings["basedir"];

        public static void Info(string data)
        {
            using (StreamWriter writer = File.AppendText(baseDir+"Log\\DHCC_Infolog.csv"))
            {
                writer.Write(System.DateTime.Now + " : " + data + "\n");
            }
        }

        public static void Error(Exception data)
        {
            using (StreamWriter writer = File.AppendText(baseDir + "Log\\DHCC_Exceptionlog.csv"))
            {
                writer.Write(System.DateTime.Now + " : " + data + "\n");
            }
        }

        public static void DumpTransformed(string data)
        {
            using (StreamWriter writer = File.AppendText(baseDir + "Log\\DHCC_DumpTransformed.csv"))
            {
                writer.Write(data+"\n");
            }
        }


    }
}
