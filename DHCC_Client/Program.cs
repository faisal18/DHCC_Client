using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DHCC_Client
{
    class Program
    {
        static void Main(string[] args)
        {

            string method = System.Configuration.ConfigurationManager.AppSettings.Get("method");
            if (method == "daily")
            {
                Dhcc.StartDaily();
                //Dhcc.Upload_File(Dhcc.LMU_Parser(@"C:\tmp\DHA\techsupport_clinician_20200408183806.csv"));
            }
            else if (method == "manual")
            {
                Dhcc.Start();

            }

            //Dhcc.RemoveDuplicates(@"C:\tmp\DHA\For Rana\Sheryan_Clinicians_20200518001500_LMUPassed.csv");

            //foreach (string sp in File.ReadAllLines(@"C:\tmp\DHA\spcode.csv"))
            //{
            //    Console.WriteLine(Dhcc.GetLMURecordForSpecialities(sp, "2"));
            //}
            //Console.Read();
        }
    }
}
