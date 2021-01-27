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

        private static string info = ConfigurationManager.AppSettings["info"];
        private static string error = ConfigurationManager.AppSettings["error"];
        private static string dump = ConfigurationManager.AppSettings["dump"];


        public static void Info(string data)
        {
            using (StreamWriter writer = File.AppendText(baseDir+"Log\\"+info+".csv"))
            {
                writer.Write(System.DateTime.Now + " : " + data + "\n");
            }
        }

        public static void Error(Exception data)
        {
            using (StreamWriter writer = File.AppendText(baseDir + "Log\\" + error + ".csv"))
            {
                writer.Write(System.DateTime.Now + " : " + data + "\n");
            }
        }

        public static void DumpTransformed(string data)
        {
            using (StreamWriter writer = File.AppendText(baseDir + "Log\\" + dump + ".csv"))
            {
                //writer.Write(data+"\n");
                writer.Write(System.DateTime.Now + " : " + data + "\n");

            }
        }


    }
}
