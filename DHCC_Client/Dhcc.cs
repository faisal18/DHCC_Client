using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Configuration;

namespace DHCC_Client
{
    class Dhcc
    {

        #region Variables
        private static string clin_path;
        private static string facil_path;
        private static string reference_absloute;
        private static string[] global_start;
        private static string[] global_end;
        private static string Queue_dir = System.Configuration.ConfigurationManager.AppSettings["EmailQueue"];
        private static string baseDir = ConfigurationManager.AppSettings["basedir"];
        #endregion

        #region DHCC Control
        public static void Start()
        {
            try
            {

                bool to_iterate = true;
                bool is_QA = false;
                string Days = "1";

                string strFrom = System.Configuration.ConfigurationManager.AppSettings.Get("StrFrom");
                string strTo = System.Configuration.ConfigurationManager.AppSettings.Get("StrTo");
                string enviorment = System.Configuration.ConfigurationManager.AppSettings.Get("enviorment");


                if (enviorment.ToLower() == "qa")
                {
                    is_QA = true;
                }
                else if (enviorment.ToLower() == "production")
                {
                    is_QA = false;
                }

                Control_Unit(to_iterate, true, strFrom, strTo, Days, is_QA);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
        public static void StartDaily()
        {
            try
            {

                string Days = "1";
                string strFrom = System.DateTime.Now.AddDays(-1).ToString("dd-MM-yyyy");
                string strTo = System.DateTime.Now.ToString("dd-MM-yyyy");
                bool is_QA = false;


                //string enviorment = System.Configuration.ConfigurationManager.AppSettings.Get("enviorment");
                //if (enviorment.ToLower() == "qa")
                //{
                //    is_QA = true;
                //}
                //else if (enviorment.ToLower() == "production")
                //{
                //    is_QA = false;
                //}

                Control_Unit(false, true, strFrom, strTo, Days, is_QA);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
        public static string Control_Unit(bool to_iterate, bool to_upload, string strFrom, string strTo, string Days, bool is_QA)
        {

            string local_path = System.Configuration.ConfigurationManager.AppSettings.Get("basedir");
            string result = string.Empty;


            //clin_path = local_path + "techsupport_clinician_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            //facil_path = local_path + "techsupport_facility_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            //reference_absloute = local_path + "techsupport_Reference_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt";

            clin_path = local_path + "DHCC_Clinicians_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            facil_path = local_path + "DHCC_facility_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            reference_absloute = local_path + "DHCC_Reference_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt";

            Logger.Info("DHCC Process started");
            Logger.Info("Clinician file path " + clin_path);
            Logger.Info("Facilities file path " + facil_path);

            try
            {


                if (strFrom.Length > 1)
                {
                    if (File.Exists(clin_path) == false || File.Exists(facil_path) == false)
                    {
                        if (Create_Files(clin_path, true) == true && Create_Files(facil_path, false) == true)
                        {
                            if (to_iterate == false)
                            {
                                if (!is_QA)
                                {
                                    result = Run_Process(strFrom, strTo);
                                }
                                else if (is_QA)
                                {
                                    result = Run_Process_QA(strFrom, strTo);
                                }

                                //clin_path = LMU_Parser(clin_path);

                                if (to_upload)
                                {
                                    RemoveDuplicates(clin_path);
                                    Upload_File(LMU_Parser(clin_path));
                                    //Upload_data(clin_path);
                                    move_files(clin_path);
                                }
                                else
                                {
                                    move_files(clin_path);
                                    move_files(facil_path);
                                    Directory.Delete(local_path, true);
                                }
                            }
                            else if (to_iterate == true)
                            {
                                result = Run_Iterative(Days, strFrom, is_QA);
                                if (to_upload)
                                {
                                    RemoveDuplicates(clin_path);
                                    Upload_File(LMU_Parser(clin_path));
                                    //Upload_data(clin_path);
                                }
                                else
                                {
                                    move_files(clin_path);
                                    move_files(facil_path);
                                    move_files(reference_absloute);
                                    //Directory.Delete(local_path, true);
                                }
                            }
                        }
                    }
                }
                else
                {
                    result = "Please select dates";
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return ex.Message;
            }
        }
        private static string Run_Iterative(string Days, string strFrom, bool is_QA)
        {
            Logger.Info("Process Controller started");

            try
            {
                get_DateRanges(Days, strFrom);
                string result_builder = string.Empty;
                for (int i = 0; i < global_start.Length; i++)
                {
                    if (!is_QA)
                    {
                        result_builder += Run_Process(global_start[i], global_end[i]);
                    }
                    else if (is_QA)
                    {
                        result_builder += Run_Process_QA(global_start[i], global_end[i]);
                    }
                }
                using (StreamWriter resulter = new StreamWriter(reference_absloute))
                {
                    resulter.Write(result_builder);
                }
                Logger.Info("Process Controller completed");
                return "Process completed for " + global_start.Length + " sub set of dates";
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return ex.Message;
            }
        }
        private static string Run_Process(string strFrom, string strTo)
        {
            Logger.Info("Process Excecution started for " + strFrom + " to " + strTo);
            Console.WriteLine("Process Excecution started for " + strFrom + " to " + strTo);

            string result = string.Empty;
            string password_HCP = string.Empty;
            string password_local = string.Empty;
            string calculatedtoken = "DHCC-" + DateTime.Now.ToString("ddMMyyyy");

            try
            {

                EncryptionDecryptionHelper encryptionHelper = new EncryptionDecryptionHelper();
                DHCCHCPService.HCPServiceSoapClient hcpClient = new DHCCHCPService.HCPServiceSoapClient();
                DHCCHCPService.Authentication authentication = new DHCCHCPService.Authentication();
                DHCCHCPService.InputParam inputParam = new DHCCHCPService.InputParam();


                string encodeid = encryptionHelper.EncryptMain(calculatedtoken, "CERTNAME_PROD");
                Logger.Info("Encoded id :" + encodeid);
                authentication.secid = encodeid;
                inputParam.dateFrom = strFrom;
                inputParam.dateTo = strTo;

                Logger.Info("Get Professtionals called");
                DHCCHCPService.HCP[] listHCP = hcpClient.GetProfessionals(authentication, inputParam);

                StringBuilder sb = new StringBuilder();
                StringBuilder sb2 = new StringBuilder();




                if (listHCP != null)
                {
                    Logger.Info("ListHCP is not NULL");
                    if (listHCP.Length > 0)
                    {
                        Logger.Info("Get Professtionals returned with " + listHCP.Length + " records for date range Start:" + strFrom + " End:" + strTo);

                        using (StreamWriter clinician = File.AppendText(clin_path))
                        {
                            using (StreamWriter facility = File.AppendText(facil_path))
                            {
                                facility.AutoFlush = true;
                                clinician.AutoFlush = true;


                                for (int i = 0; i < listHCP.Length; i++)
                                {
                                    DHCCHCPService.Facility[] obj_facility = listHCP[i].facilities;
                                    facility.Write(TransformFacility(obj_facility, listHCP[i].License));

                                    try
                                    {
                                        if (listHCP[i].password != null)
                                        {

                                            if (listHCP[i].password.Length > 0)
                                            {
                                                password_HCP = encryptionHelper.DecryptMain(listHCP[i].password, "CERTNAME_PROD");
                                                password_local = Encrypt(password_HCP);
                                            }
                                        }
                                        else if (listHCP[i].password == null)
                                        {
                                            Logger.Info("Empty password returned for license " + listHCP[i].License);
                                            password_local = string.Empty;
                                        }

                                        Logger.Info("Row No:" + i);
                                        Logger.DumpTransformed(strFrom + "," + strTo + "," + listHCP[i].License + "," + listHCP[i].FullName + "," + listHCP[i].username + "," + password_local + "," + listHCP[i].FacilityLicense
                                               + "," + listHCP[i].FacilityName + "," + listHCP[i].Location + "," + listHCP[i].ActiveFrom + "," + listHCP[i].ActiveTo + "," + listHCP[i].IsActive + "," + listHCP[i].Source
                                               + "," + listHCP[i].SpecialtyID1 + "," + listHCP[i].SpecialtyDescription + "," + listHCP[i].Gender + "," + listHCP[i].Nationality + "," + listHCP[i].Email
                                               + "," + listHCP[i].PhoneNumber + "," + listHCP[i].SpecialtyID2 + "," + listHCP[i].SpecialtyID3 + "," + listHCP[i].HCType);


                                        if (listHCP[i].License != null && listHCP[i].FullName != null && listHCP[i].IsActive != null && listHCP[i].Source != null
                                            && listHCP[i].HCType != null)
                                        {
                                            if (listHCP[i].License.Trim().Length > 0 && listHCP[i].FullName.Trim().Length > 0 && listHCP[i].IsActive.Trim().Length > 0
                                                && listHCP[i].Source.Trim().Length > 0 && listHCP[i].HCType.Trim().Length > 0)
                                            {

                                                //New rules as of 27th January 2021 from email "DHCC clinician Analysis"
                                                if (listHCP[i].SpecialtyID1.ToUpper() != "NP-O" && listHCP[i].SpecialtyID1.ToUpper() != "MR")
                                                {

                                                    //NEW RULE as of 09Feb2021 to remove clinician whose ActiveTo dates are not met with current date even after adding 30 days grace period
                                                    string cunt = Convert.ToDateTime(ConvertDate(listHCP[i].ActiveTo)).AddDays(30).ToString("MM/dd/yyyy hh:mm:ss");
                                                    bool cunt_b = false;
                                                    if(Convert.ToDateTime(cunt)<DateTime.Now)
                                                    {
                                                        cunt_b = true;
                                                        Logger.Info("This clinician " + listHCP[i].License + " will be excluded due to the new rule applied 09Feb2021");
                                                    }

                                                    if (!cunt_b)
                                                    {
                                                        sb.Append(
                                                            //ConvertDate(strFrom) + "," +
                                                            //ConvertDate(strTo) + "," +

                                                            CheckComma2(listHCP[i].License) + "," +
                                                            CheckComma2(listHCP[i].FullName) + "," +
                                                            CheckComma2(listHCP[i].username) + "," +

                                                            password_local + "," +

                                                            CheckComma2(listHCP[i].FacilityLicense) + "," +
                                                            CheckComma2(listHCP[i].FacilityName) + "," +
                                                            CheckComma2(listHCP[i].Location) + "," +

                                                            ConvertDate(listHCP[i].ActiveFrom) + "," +

                                                            //New rules as of 27th January 2021 from email "DHCC clinician Analysis"
                                                            Convert.ToDateTime(ConvertDate(listHCP[i].ActiveTo)).AddDays(30).ToString("MM/dd/yyyy hh:mm:ss") + "," +

                                                            CheckComma2(listHCP[i].IsActive) + "," +
                                                            CheckComma2(listHCP[i].Source) + "," +
                                                            CheckComma2(listHCP[i].SpecialtyID1) + "," +
                                                            CheckComma2(listHCP[i].SpecialtyDescription) + "," +
                                                            CheckComma2(listHCP[i].Gender) + "," +
                                                            CheckComma2(listHCP[i].Nationality) + "," +
                                                            CheckComma2(listHCP[i].Email) + "," +
                                                            CheckComma2(listHCP[i].PhoneNumber) + "," +
                                                            CheckComma2(listHCP[i].SpecialtyID2) + "," +
                                                            CheckComma2(listHCP[i].SpecialtyID3) + "," +
                                                            CheckComma2(listHCP[i].HCType) + "," +
                                                            "" + "\n"
                                                            );
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Logger.Info("Null value found");
                                            Logger.Info("License: " + listHCP[i].License + " Fullname: " + listHCP[i].FullName + " IsActive: " + listHCP[i].IsActive + " Source: " + listHCP[i].Source
                                                + " HCP Type: " + listHCP[i].HCType);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(ex);
                                        Logger.Info("Inner exception occured on " + listHCP[i].License);
                                    }


                                }// For loop ending

                            }
                            Logger.Info("Appending records to Clinician file");
                            clinician.Write(sb);
                        }
                        //Logger.Info("\nRecords Found:" + listHCP.Length + " Date From: " + strFrom + " To: " + strTo);
                        result = "\nRecords Found:" + listHCP.Length + " Date From: " + strFrom + " To: " + strTo;
                    }
                    else
                    {
                        Logger.Info("\nNo records found in date From: " + strFrom + " To: " + strTo);
                        result = "\nNo records found in date From: " + strFrom + " To: " + strTo;
                    }
                }
                else
                {
                    Logger.Info("\nLIST HCP returned NULL date From: " + strFrom + " To: " + strTo);
                    result = "\nLIST HCP returned NULL date From: " + strFrom + " To: " + strTo;
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Logger.Info("\nException Occured in date from: " + strFrom + " to: " + strTo + "\nError: " + ex.Message);
                return "\nException Occured in date from: " + strFrom + " to: " + strTo + "\nError: " + ex.Message;
            }
        }
        private static string Run_Process_QA(string strFrom, string strTo)
        {
            Logger.Info("Process Excecution started");
            string result = string.Empty;
            string password_HCP = string.Empty;
            string password_local = string.Empty;
            string calculatedtoken = "DHCC-" + DateTime.Now.ToString("ddMMyyyy");

            try
            {

                EncryptionDecryptionHelper encryptionHelper = new EncryptionDecryptionHelper();
                DHCCHCPService_QA.HCPServiceSoapClient hcpClient = new DHCCHCPService_QA.HCPServiceSoapClient();
                DHCCHCPService_QA.Authentication authentication = new DHCCHCPService_QA.Authentication();
                DHCCHCPService_QA.InputParam inputParam = new DHCCHCPService_QA.InputParam();


                string encodeid = encryptionHelper.EncryptMain(calculatedtoken, "CERTNAME_QA");
                Logger.Info("Encoded id :" + encodeid);
                authentication.secid = encodeid;
                inputParam.dateFrom = strFrom;
                inputParam.dateTo = strTo;

                Logger.Info("Get Professtionals called");
                DHCCHCPService_QA.HCP[] listHCP = hcpClient.GetProfessionals(authentication, inputParam);


                StringBuilder sb = new StringBuilder();
                StringBuilder sb2 = new StringBuilder();


                if (listHCP != null)
                {
                    Logger.Info("ListHCP is not NULL");
                    if (listHCP.Length > 0)
                    {
                        foreach (var item in listHCP)
                        {
                            Logger.Info(item.ActiveFrom + "," + item.ActiveTo + "," + item.DHCCSpecialty1 + "," + item.DHCCSpecialty2 + "," + item.DHCCSpecialty3 + "," + item.Email + "," + item.EmiratesID + "," + item.facilities + "," + item.FacilityLicense + "," + item.FacilityName + "," + item.FullName + "," + item.Gender + "," + item.HCType + "," + item.IsActive + "," + item.License + "," + item.Location + "," + item.Nationality + "," + item.PassportIssuingCountry + "," + item.PassportNumber + "," + item.password + "," + item.PhoneNumber + "," + item.Qualification + "," + item.Source + "," + item.SpecialtyDescription + "," + item.SpecialtyID1 + "," + item.SpecialtyID2 + "," + item.SpecialtyID3 + "," + item.username);

                        }

                        Logger.Info("Get Professtionals returned with " + listHCP.Length + " records for date range Start:" + strFrom + " End:" + strTo);

                        using (StreamWriter clinician = File.AppendText(clin_path))
                        {
                            using (StreamWriter facility = File.AppendText(facil_path))
                            {
                                facility.AutoFlush = true;
                                clinician.AutoFlush = true;


                                for (int i = 0; i < listHCP.Length; i++)
                                {

                                    password_HCP = encryptionHelper.DecryptMain(listHCP[i].password, "CERTNAME_QA");
                                    password_local = Encrypt(password_HCP);
                                    //string password_local_decrypt = Decrypt(password_local);

                                    //DateTime startd = DateTime.ParseExact(strFrom, "dd-MM-yyyy", null);
                                    //string strFrom_New = startd.ToString("MM/dd/yyyy hh:mm:ss");

                                    //DateTime endd = DateTime.ParseExact(strTo, "dd-MM-yyyy", null);
                                    //string strTo_New = endd.ToString("MM/dd/yyy hh:mm:ss");

                                    //string activefromDatedata = string.Empty;
                                    //string activetoDatedata = string.Empty;

                                    //DateTime mydate = Convert.ToDateTime(listHCP[i].ActiveFrom);
                                    //activefromDatedata = mydate.ToString("MM/dd/yyyy hh:mm:ss");

                                    //DateTime ActiveTo = Convert.ToDateTime(listHCP[i].ActiveTo);
                                    //activetoDatedata = ActiveTo.ToString("MM/dd/yyyy hh:mm:ss");

                                    //sb.Append(strFrom_New + "," + strTo_New + "," + listHCP[i].License + "," + listHCP[i].FullName + "," + listHCP[i].username + "," + password_local + "," + listHCP[i].FacilityLicense
                                    //    + "," + listHCP[i].FacilityName + "," + listHCP[i].Location + "," + activefromDatedata + "," + activetoDatedata + "," + listHCP[i].IsActive + "," + listHCP[i].Source
                                    //     + "," + listHCP[i].SpecialtyID1 + "," + listHCP[i].SpecialtyDescription + "," + listHCP[i].Gender + "," + listHCP[i].Nationality + "," + listHCP[i].Email
                                    //     + "," + listHCP[i].PhoneNumber + "," + listHCP[i].SpecialtyID2 + "," + listHCP[i].SpecialtyID3 + "," + listHCP[i].HCType + "\n");

                                    //if (listHCP[i].facilities != null)
                                    //{
                                    //    DHCCHCPService_QA.Facility[] listFacility = listHCP[i].facilities;
                                    //    if (listFacility.Length > 0)
                                    //    {
                                    //        Logger.Info("Facilites records found. Records:" + listFacility.Length);

                                    //        for (int j = 0; j < listFacility.Length; j++)
                                    //        {
                                    //            sb2.Append(strFrom + "," + strTo + "," + listHCP[i].License + "," + listHCP[i].FullName + "," + listFacility[j].FacilityLicense + "," + listFacility[j].FacilityName + "\n");
                                    //        }
                                    //        Logger.Info("Appending records to facilities file");
                                    //        facility.Write(sb2);
                                    //    }
                                    //}

                                    if (listHCP[i].License != null && listHCP[i].FullName != null && listHCP[i].IsActive != null && listHCP[i].Source != null
                                        && listHCP[i].HCType != null)
                                    {
                                        if (listHCP[i].License.Trim().Length > 0 && listHCP[i].FullName.Trim().Length > 0 && listHCP[i].IsActive.Trim().Length > 0
                                            && listHCP[i].Source.Trim().Length > 0 && listHCP[i].HCType.Trim().Length > 0)
                                        {
                                            sb.Append(
                                                //ConvertDate(strFrom) + "," +
                                                //ConvertDate(strTo) + "," +

                                                CheckComma(listHCP[i].License) + "," +
                                                CheckComma(listHCP[i].FullName) + "," +
                                                CheckComma(listHCP[i].username) + "," +

                                                password_local + "," +

                                                CheckComma(listHCP[i].FacilityLicense) + "," +
                                                CheckComma(listHCP[i].FacilityName) + "," +
                                                CheckComma(listHCP[i].Location) + "," +

                                                ConvertDate(listHCP[i].ActiveFrom) + "," +
                                                ConvertDate(listHCP[i].ActiveTo) + "," +

                                                CheckComma(listHCP[i].IsActive) + "," +
                                                CheckComma(listHCP[i].Source) + "," +
                                                CheckComma(listHCP[i].SpecialtyID1) + "," +
                                                CheckComma(listHCP[i].SpecialtyDescription) + "," +
                                                CheckComma(listHCP[i].Gender) + "," +
                                                CheckComma(listHCP[i].Nationality) + "," +
                                                CheckComma(listHCP[i].Email) + "," +
                                                CheckComma(listHCP[i].PhoneNumber) + "," +
                                                CheckComma(listHCP[i].SpecialtyID2) + "," +
                                                CheckComma(listHCP[i].SpecialtyID3) + "," +
                                                CheckComma(listHCP[i].HCType) + "\n"
                                                );
                                        }
                                    }
                                    else
                                    {
                                        Logger.Info("Null value found");
                                        Logger.Info("License: " + listHCP[i].License + " Fullname: " + listHCP[i].FullName + " IsActive: " + listHCP[i].IsActive + " Source: " + listHCP[i].Source
                                            + " HCP Type: " + listHCP[i].HCType);
                                    }

                                }
                            }
                            Logger.Info("Appending records to Clinician file");
                            clinician.Write(sb);
                        }
                        Logger.Info("\nRecords Found:" + listHCP.Length + " Date From: " + strFrom + " To: " + strTo);
                        result = "\nRecords Found:" + listHCP.Length + " Date From: " + strFrom + " To: " + strTo;
                    }
                    else
                    {
                        Logger.Info("\nNo records found in date From: " + strFrom + " To: " + strTo);
                        result = "\nNo records found in date From: " + strFrom + " To: " + strTo;
                    }
                }
                else
                {
                    Logger.Info("\nLIST HCP returned NULL date From: " + strFrom + " To: " + strTo);
                    result = "\nLIST HCP returned NULL date From: " + strFrom + " To: " + strTo;
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return "\nException Occured in date from: " + strFrom + " to: " + strTo + "\nError: " + ex.Message;
            }
        }
        public static string TransformFacility(DHCCHCPService.Facility[] obj_facility, string ClinicianLicense)
        {
            string result = string.Empty;
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < obj_facility.Length; i++)
                {
                    sb.Append(
                        CheckComma(ClinicianLicense) + "," +
                        CheckComma(obj_facility[i].City) + "," +
                        CheckComma(obj_facility[i].Country) + "," +
                        CheckComma(obj_facility[i].Email) + "," +
                        CheckComma(obj_facility[i].FacilityLicense) + "," +
                        CheckComma(obj_facility[i].FacilityName) + "," +
                        CheckComma(obj_facility[i].FacilityType) + "," +
                        CheckComma(obj_facility[i].Fax) + "," +
                        CheckComma(obj_facility[i].Jurisdiction) + "," +
                        CheckComma(obj_facility[i].LegalEntity) + "," +
                        CheckComma(ConvertDate(obj_facility[i].LicenseFrom)) + "," +
                        CheckComma(obj_facility[i].LicenseStatus) + "," +
                        CheckComma(ConvertDate(obj_facility[i].LicenseTo)) + "," +
                        CheckComma(ListtoStirng(obj_facility[i].listActivities)) + "," +
                        CheckComma(obj_facility[i].Mobile) + "," +
                        CheckComma(obj_facility[i].Street) + "," +
                        CheckComma(obj_facility[i].Website) + "\n"
                        );
                }

                result = sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return result;
        }

        public static void RemoveDuplicates(string filepath)
        {
            Logger.Info("Removing Duplicates");
            try
            {
                string[] linese = File.ReadAllLines(filepath);
                File.WriteAllLines(filepath, linese.Distinct().ToArray());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            Logger.Info("Duplicates Removed");

        }

        #endregion

        #region LMUParser
        public static string LMU_Parser(string filepath)
        {
            string newfilename = Path.GetFileNameWithoutExtension(filepath);
            newfilename = newfilename + "_LMUPassed.csv";

            try
            {
                string[] lines = File.ReadAllLines(filepath);

                StringBuilder sb = new StringBuilder();
                sb.Append("License,name,Username,Password,Facility License,Facility Name,area,Active From,Active To,is active,source," +
                            "Specialty ID 1,Specialty,Gender,Nationality,Email,Phone,Specialty ID 2,Specialty ID 3,type,Old License\n");

                string latestversion = GetLMULatest();
                string Specialitiesversion = GetLMUSpecialitesLatest();

                if (lines.Length > 0)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {


                        string[] data = lines[i].Split(',');


                        string license = data[0];
                        string license_start = data[7];
                        string license_end = data[8];
                        string isActive = data[9];
                        string source = data[10];

                        string SpecialityID = data[11];
                        string Speciality = data[12];

                        Console.WriteLine("LMU iteration " + i + " clinician license " + license);
                        Logger.Info("LMU iteration " + i + " clinician license " + license);



                        GateParams obj = GetClinicianRecord(license, latestversion);
                        obj.DHA_Input = CheckActive_Reverse(isActive);
                        obj = GetTruthTable(obj);
                        isActive = obj.Out_isActive;
                        source = obj.Out_Source;
                        Speciality = GetLMURecordForSpecialities(SpecialityID, Specialitiesversion);

                        license_start = ConvertDate_LMU(license_start);
                        license_end = ConvertDate_LMU(license_end);



                        sb.Append(
                            data[0] + "," +
                            data[1] + "," +
                            data[2] + "," +
                            data[3] + "," +
                            data[4] + "," +
                            data[5] + "," +
                            data[6] + "," +
                            license_start + "," +
                            license_end + "," +
                            isActive + "," +
                            source + "," +
                            data[11] + "," +
                            Speciality + "," +
                            data[13] + "," +
                            data[14] + "," +
                            data[15] + "," +
                            data[16] + "," +
                            data[17] + "," +
                            data[18] + "," +
                            data[19] + "," +
                            data[20] + "\n"
                            );
                    }


                    Console.WriteLine("Writting file");


                    using (StreamWriter wrtier = File.CreateText(baseDir + "\\" + newfilename))
                    {
                        wrtier.Write(sb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return baseDir + "\\" + newfilename;
        }
        public static string GetLMULatest()
        {
            string result = string.Empty;
            try
            {
                string url = ConfigurationManager.AppSettings.Get("LMU_URL") + ConfigurationManager.AppSettings.Get("LMU_Clinician_Latest");
                string username = ConfigurationManager.AppSettings.Get("LMU_Prod_Username");
                string token = ConfigurationManager.AppSettings.Get("LMU_Prod_Token");
                result = PostCall_ByBody(url, "", token, username, false);
                //result = "7539";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return result;
        }
        public static string GetLMUSpecialitesLatest()
        {
            string result = string.Empty;
            try
            {
                string url = ConfigurationManager.AppSettings.Get("LMU_URL") + ConfigurationManager.AppSettings.Get("LMU_Specialities_Latest");
                string username = ConfigurationManager.AppSettings.Get("LMU_Prod_Username");
                string token = ConfigurationManager.AppSettings.Get("LMU_Prod_Token");
                result = PostCall_ByBody(url, "", token, username, false);
                //result = "7539";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return result;
        }
        public static GateParams GetClinicianRecord(string data, string latestversion)
        {
            string result = string.Empty;
            GateParams obj = new GateParams();

            try
            {
                string url = ConfigurationManager.AppSettings.Get("LMU_URL") + ConfigurationManager.AppSettings.Get("LMU_Clinician"); ;
                string username = ConfigurationManager.AppSettings.Get("LMU_Prod_Username");
                string token = ConfigurationManager.AppSettings.Get("LMU_Prod_Token");
                string license = data;


                //string body = "{\r\n \"oldVersion\": 0,\"targetVersion\": " + GetLMULatest() + ",\r\n \"param\" : \"license=" + license + "\" \r\n}";
                //string body = "{\r\n \"oldVersion\": 0,\"targetVersion\": " + latestversion + ",\r\n \"param\" : \"license=" + license + "\" \r\n}";

                result = PostCall_ByBody(url, data, token, username, true);



                JObject yo = JObject.Parse(result);
                if (yo["content"] != null)
                {
                    if (yo["content"].First != null)
                    {
                        string status = yo["content"].First["status"].ToString();
                        string isActive = yo["content"].First["values"]["isActive"].ToString();
                        string source = yo["content"].First["values"]["source"].ToString();

                        obj.LMU_isActive = isActive;
                        obj.LMU_Source = source;
                        obj.LMU_Status = status;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return obj;
        }
        public static string GetLMURecordForSpecialities(string data, string Specialitiesversion)
        {
            string result = string.Empty;
            string description = string.Empty;
            try
            {
                string url = ConfigurationManager.AppSettings.Get("LMU_URL") + ConfigurationManager.AppSettings.Get("LMU_Specialities"); ;
                string username = ConfigurationManager.AppSettings.Get("LMU_Prod_Username");
                string token = ConfigurationManager.AppSettings.Get("LMU_Prod_Token");
                string body = "{\r\n \"oldVersion\": 0,\"targetVersion\": " + Specialitiesversion + ",\r\n \"param\" : \"specialtyId=" + data + "\" \r\n}";


                if (data != null)
                {
                    if (data.Length > 1)
                    {
                        result = PostCall_ByBody(url, body, token, username, false);
                        Logger.Info(result);
                        JObject yo = JObject.Parse(result);
                        if (yo["content"] != null)
                        {
                            if (yo["content"].First != null)
                            {
                                string specialty = yo["content"].First["values"]["specialty"].ToString();
                                description = specialty;
                            }
                        }
                    }
                }



            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return description;
        }


        public static GateParams GetTruthTable(GateParams obj)
        {
            try
            {


                if (obj.LMU_isActive != null && obj.LMU_Source != null)
                {

                    if (obj.DHA_Input.ToUpper() == "TRUE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "ALL")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "ALL";
                    }

                    else if (obj.DHA_Input.ToUpper() == "FALSE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "ALL")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "HAAD";
                    }


                    else if (obj.DHA_Input.ToUpper() == "TRUE" && obj.LMU_isActive.ToUpper() == "FALSE" && obj.LMU_Source.ToUpper() == "ALL")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "DHA";
                    }

                    else if (obj.DHA_Input.ToUpper() == "TRUE" && obj.LMU_isActive.ToUpper() == "FALSE" && obj.LMU_Source.ToUpper() == "DHA")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "DHA";
                    }

                    else if (obj.DHA_Input.ToUpper() == "FALSE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "DHA")
                    {
                        obj.Out_isActive = "FALSE";
                        obj.Out_Source = "DHA";
                    }

                    else if (obj.DHA_Input.ToUpper() == "FALSE" && obj.LMU_isActive.ToUpper() == "FALSE" && obj.LMU_Source.ToUpper() == "DHA")
                    {
                        obj.Out_isActive = "FALSE";
                        obj.Out_Source = "DHA";
                    }

                    else if (obj.DHA_Input.ToUpper() == "TRUE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "DHA")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "DHA";
                    }

                    else if (obj.DHA_Input.ToUpper() == "TRUE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "HAAD")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "ALL";
                    }

                    else if (obj.DHA_Input.ToUpper() == "FALSE" && obj.LMU_isActive.ToUpper() == "TRUE" && obj.LMU_Source.ToUpper() == "HAAD")
                    {
                        obj.Out_isActive = "TRUE";
                        obj.Out_Source = "HAAD";
                    }

                }


                else if (obj.DHA_Input.ToUpper() == "TRUE")
                {
                    obj.Out_isActive = "TRUE";
                    obj.Out_Source = "DHA";
                }


                else if (obj.DHA_Input.ToUpper() == "FALSE")
                {
                    obj.Out_isActive = "FALSE";
                    obj.Out_Source = "DHA";
                }




            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return obj;
        }
        public static string CheckActive_Reverse(string data)
        {
            string active = string.Empty;

            if (data.ToString() == "Deactivated")
                active = "False";
            if (data.ToString() == "Active")
                active = "True";
            else
                active = "False";

            return active;
        }
        public static string PostCall_ByBody(string URL, string postdata, string accessKey, string username, bool isGet)
        {
            string result = string.Empty;
            bool OKAY = false;
            int counter = 0;
            //int counter_limit = int.Parse(ConfigurationManager.AppSettings.Get("counter_limit"));
            int counter_limit = 1;

            try
            {
                while (!OKAY)
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                            //wc.Headers[HttpRequestHeader.Authorization] = staticToken;

                            wc.Headers.Set("username", username);
                            wc.Headers.Set("access-key", accessKey);

                            Logger.Info("Sheryan called at " + DateTime.Now);
                            if (!isGet)
                            {
                                result = wc.UploadString(URL, postdata);
                            }
                            if (isGet)
                            {
                                string complete = URL + "?queryString=values.license|EQ|" + postdata;
                                result = wc.DownloadString(complete);
                            }

                            //CustomLog.Info(result);
                            //Console.WriteLine(result);

                            if (result != null)
                                if (result.Length > 0)
                                {
                                    //JObject yo = JObject.Parse(result);
                                    //if (yo["ReturnCode"] != null)
                                    //{
                                    //    Console.WriteLine("Return Code: " + yo["ReturnCode"].ToString());
                                    //    if (yo["ReturnCode"].ToString() == "00")
                                    //    {
                                    //        OKAY = true;
                                    //        //("Results found for data: " + postdata);
                                    //        //CustomLog.Info("Results found for data: " + postdata);
                                    //    }
                                    //    if (yo["ReturnCode"].ToString() == "10" || yo["ReturnCode"].ToString() == "07")
                                    //    {
                                    //        OKAY = true;
                                    //        //("Results found for data: " + postdata);
                                    //        //CustomLog.Info(yo["ReturnMessage"].ToString());
                                    //    }
                                    //}
                                    //else
                                    //{
                                    //    Console.WriteLine("Result: " + yo.ToString());
                                    //    //CustomLog.Info(yo.ToString());
                                    //    Console.WriteLine(yo.ToString());

                                    //}

                                    OKAY = true;

                                }
                                else if (counter > counter_limit)
                                {
                                    OKAY = true;
                                    Console.WriteLine("Tried hitting the service " + counter_limit + " times but no results found");

                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Occured in PostCall_ByBody !\n" + result);
                return ex.Message;
            }
        }
        private static string ConvertDate_LMU(string date)
        {
            string resultdate = date;
            try
            {
                if (date != null)
                {
                    if (date.Length > 0)
                    {
                        resultdate = Convert.ToDateTime(date).ToString("yyyy-MM-dd");
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return resultdate;
        }

        #endregion

        #region helper_functions

        public static string Encrypt(string clearText)
        {
            try
            {
                string EncryptionKey = "dhcc_client";
                byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                            cs.Close();
                        }
                        clearText = Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return clearText;
        }
        public static string Decrypt(string cipherText)
        {
            try
            {
                string EncryptionKey = "dhcc_client";
                cipherText = cipherText.Replace(" ", "+");
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return cipherText;
        }

        public static string ConvertDate(string Rdate)
        {

            string[] formats = {
                        "dd/MM/yyyy hh:mm:ss tt"
                     ,  "dd/MM/yyyy hh:mm:ss"
                     ,  "dd/MM/yyyy hh:mm:ss t"
                     ,  "dd/MM/yyyy h:m:s t"
                     ,  "dd/MM/yyyy HH:mm:ss"
                     ,  "dd/MM/yyyy HH:mm:ss tt"
                     ,  "dd/MM/yyyy H:m:s t"
                     ,  "dd/MM/yyyy"
                     ,  "d/M/yy hh:mm:ss tt"
                     ,  "M/d/yyyy h:mm:ss tt"
                     ,  "M/d/yyyy h:mm tt"
                     ,  "MM/dd/yyyy hh:mm:ss"
                     ,  "M/d/yyyy h:mm:ss"
                     ,  "M/d/yyyy hh:mm tt"
                     ,  "M/d/yyyy hh tt"
                     ,  "M/d/yyyy h:mm"
                     ,  "M/d/yyyy h:mm"
                     ,  "MM/dd/yyyy hh:mm"
                     ,  "M/dd/yyyy hh:mm"
                     ,  "dd-MM-yyyy"
                     ,  "dd-MM-yyyy HH:mm:ss"
              };

            Logger.Info("Transforming date: " + Rdate);
            string activetoDatedata = string.Empty;
            bool isParsed = false;
            DateTime getDatefromString = DateTime.Now;

            try
            {

                if (DateTime.TryParseExact(Rdate, formats, new System.Globalization.CultureInfo("en-US"), System.Globalization.DateTimeStyles.None, out getDatefromString))
                {
                    //Logger.Info("Converted '" + Rdate + "' to " + getDatefromString + ".");
                    isParsed = true;
                }
                else
                {
                    Logger.Info("Unable to parse '" + Rdate + "' to a date.");
                }

                if (isParsed)
                {
                    activetoDatedata = getDatefromString.ToString("MM/dd/yyyy hh:mm:ss");
                }
                else
                {
                    DateTime tmpDate = DateTime.Now;
                    DateTime dtNeedParsing = DateTime.TryParse(Rdate, out tmpDate) == true ? DateTime.Parse(Rdate) : tmpDate;

                    if (tmpDate == dtNeedParsing)
                    {
                        Logger.Info(" ~~~~~~~~ not able to parse the date correctly ~~~~~~~~~~~~ ");
                    }
                    else
                    {
                        activetoDatedata = dtNeedParsing.ToString("MM/dd/yyyy hh:mm:ss");
                    }
                }
                Logger.Info("Converted '" + Rdate + "' to " + activetoDatedata + ".");
                return activetoDatedata;
            }

            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }
        private static string CheckComma(string data)
        {
            string result = string.Empty;
            try
            {
                if (data != null)
                {
                    if (data.Length > 0)
                    {
                        if (data.Contains(","))
                        {
                            result = data.Replace(',', ' ');
                        }
                        else
                        {
                            result = data;
                        }
                    }
                }
                else
                {
                    result = data;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return data;
        }
        private static string CheckComma2(string data)
        {
            string result = string.Empty;
            result = data;
            if (data != null)
            {
                if (data.Length > 0)
                {
                    if (data.Contains(","))
                    {
                        result = data.Replace(",", " ");
                    }
                    if (result.Contains("\n"))
                    {
                        result = result.Replace("\n", "\\n");
                    }
                    if (result.Contains("\r"))
                    {
                        result = result.Replace("\r", " ");
                    }
                    if (result.Contains("'"))
                    {
                        result = result.Replace("'", "''");
                    }
                    if (result.Contains("|"))
                    {
                        result = result.Replace("|", "");
                    }
                    if (result.Contains("\t"))
                    {
                        result = result.Replace("\t", "\\t");
                    }
                    if (result.Contains("\""))
                    {
                        result = result.Replace("\"", "");
                    }

                }
                else
                {
                    result = "NULL";
                }
            }
            else
            {
                result = data;
            }
            return result;
        }

        public static string Upload_File_Old(string local_file_path)
        {
            string ftpUserName = System.Configuration.ConfigurationManager.AppSettings["FTPUsername"];
            string ftpPassword = System.Configuration.ConfigurationManager.AppSettings["FTPPassword"];
            string FTPServerPath = System.Configuration.ConfigurationManager.AppSettings["FTPLocalPath"];
            string ftpServerIP = System.Configuration.ConfigurationManager.AppSettings["FTPHost"];
            FileInfo localFileInfo = new FileInfo(local_file_path);
            Logger.Info("Uploading file " + Path.GetFileName(local_file_path) + " to address " + ftpServerIP + "/Pending/" + Path.GetFileName(local_file_path));
            FileStream fs = null;
            Stream uploadStream = null;
            Logger.Info("Uploading File " + local_file_path + " to server");
            try
            {

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + ftpServerIP + "//Pending/" + Path.GetFileName(local_file_path));
                Logger.Info("Create WebRequest - Success");
                request.Credentials = new NetworkCredential(ftpUserName, ftpPassword);
                request.Method = WebRequestMethods.Ftp.UploadFile;

                request.UseBinary = false;
                request.UsePassive = false;

                request.ContentLength = localFileInfo.Length;
                // The buffer size is set to 2kb
                int bufferrLength = 2048;
                byte[] _Buffer = new byte[bufferrLength];
                int contentLength;

                fs = localFileInfo.OpenRead();
                uploadStream = request.GetRequestStream();
                if (uploadStream != null)
                {
                    Logger.Info("Get FTPWebRequest - Success");
                    contentLength = fs.Read(_Buffer, 0, bufferrLength);

                    while (contentLength != 0)
                    {

                        uploadStream.Write(_Buffer, 0, contentLength);
                        contentLength = fs.Read(_Buffer, 0, bufferrLength);
                    }
                }
                uploadStream.Close();
                fs.Close();

                return "";
            }

            catch (Exception ex)
            {
                Logger.Error(ex);
                return ex.Message;
            }
            finally
            {
                uploadStream.Close();
                fs.Close();
            }

        }
        public static void Upload_File(string local_file_path)
        {
            string ftpUserName = System.Configuration.ConfigurationManager.AppSettings["FTPUsername"];
            string ftpPassword = System.Configuration.ConfigurationManager.AppSettings["FTPPassword"];
            string FTPServerPath = System.Configuration.ConfigurationManager.AppSettings["FTPLocalPath"];
            string ftpServerIP = System.Configuration.ConfigurationManager.AppSettings["FTPHost"];
            try
            {

                Logger.Info("Uploading file " + local_file_path + " to address " + ftpServerIP + "/Pending/" + Path.GetFileName(local_file_path));
                string requestpath = @"ftp://" + ftpServerIP + FTPServerPath + "//" + Path.GetFileName(local_file_path);
                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(ftpUserName, ftpPassword);
                    client.UploadFile(requestpath, WebRequestMethods.Ftp.UploadFile, local_file_path);
                }
            }
            catch (Exception ex)
            {
                Logger.Info(ex.Message);
            }

        }

        private static void Upload_data(string file)
        {
            try
            {
                string[] file_text = File.ReadAllLines(file);
                Console.WriteLine("Working on file: " + Path.GetFileNameWithoutExtension(file));
                if (file_text.Length > 1)
                {
                    StringBuilder sb = new StringBuilder();
                    string query = string.Empty;
                    for (int i = 1; i < file_text.Length; i++)
                    {
                        string[] rows = file_text[i].Split(new char[] { '|' });
                        if (rows.Length > 1)
                        {
                            Console.WriteLine("Working on line number: " + i);
                            //sb.Append
                            //(
                            query += "INSERT INTO [FS_WS_WSCTFW].[dbo].[DHCC_Clinician]([FileName],[Start_Date],[End_Date],[Clinician_License],[Clinician_Name],[Username],[Password],[Password_Encoded],[Facility_License],[Facility_Name],[Location],[Active_From],[Active_To],[Active],[Source],[SpecialityId1],[Speciality_Description],[Gender],[Nationality],[Email],[Phone],[SpecialityId2],[SpecialityId3],[type]) VALUES " +

                            "('" +
                            Path.GetFileNameWithoutExtension(file.ToString()) + "','" +
                            CheckComma2(rows[0]) + "','" +

                            CheckComma2(rows[1]) + "','" +
                            CheckComma2(rows[2]) + "','" +
                            CheckComma2(rows[3]) + "','" +
                            CheckComma2(rows[4]) + "','" +
                            "','" +
                            CheckComma2(rows[5]) + "','" +
                            CheckComma2(rows[6]) + "','" +
                            CheckComma2(rows[7]) + "','" +
                            CheckComma2(rows[8]) + "','" +
                            CheckComma2(rows[9]) + "','" +
                            CheckComma2(rows[10]) + "','" +
                            CheckComma2(rows[11]) + "','" +
                            CheckComma2(rows[12]) + "','" +
                            CheckComma2(rows[13]) + "','" +
                            CheckComma2(rows[14]) + "','" +
                            CheckComma2(rows[15]) + "','" +
                            CheckComma2(rows[16]) + "','" +
                            CheckComma2(rows[17]) + "','" +
                            CheckComma2(rows[18]) + "','" +
                            CheckComma2(rows[19]) + "','" +
                            CheckComma2(rows[20]) + "','" +
                            //CheckComma(rows[21]) + "'),";

                            CheckComma2(rows[21]) + "')\n";

                            //);
                        }

                        //Console.WriteLine("your query: " + query);
                    }

                    //query = query + sb.ToString().Remove(sb.Length - 1, 1);
                    //Insert_toDB(query, "");
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Failed to upload file to DB");
                Logger.Error(ex);
            }
        }

        private static bool Create_Files(string file_path, bool is_Clin)
        {
            bool result = false;
            Logger.Info("Creating files started");
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(file_path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                }
                using (StreamWriter sw = new StreamWriter(file_path))
                {
                    if (is_Clin)
                    {
                        //sw.Write("Start Date,End Date,Clinician License,Clinician Name,Username,Password,Facility License,Facility Name,Location,Active From,Active To,active,source," +
                        //        "Specilaity ID 1,Speciality Description,Gender,Nationality,Email,Phone,Speciality ID 2,Speciality ID 3,type,Clinician Id Old\n");
                        //sw.Write("Start Date,End Date,Clinician License,name,Username,Password,Facility License,Facility Name,Location,from,to,Active,source,"+
                        //    "Specialty ID 1,Specialty Description,Gender,Nationality,Email,Phone,Specialty ID 2,Specialty ID 3,type,Old_License\n");
                        sw.Write("License,name,Username,Password,Facility License,Facility Name,area,Active From,Active To,is active,source," +
                            "Specialty ID 1,Specialty,Gender,Nationality,Email,Phone,Specialty ID 2,Specialty ID 3,type,Old License\n");
                        result = true;
                    }
                    else if (!is_Clin)
                    {
                        sw.Write("ClinicianLicense,City,Country,Email,FacilityLicense,FacilityName,FacilityType,Fax,Jurisdiction,LegalEntity,licenseFrom,licenseStatus,licenseto,Activites,Mobile,Street,Website\n");
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                result = false;
            }
            Logger.Info("Creating files completed");

            return result;
        }
        private static bool Move_Directory(string Local_DIR, string Queue_DIR)
        {
            Logger.Info("Process Move to Queue started");
            bool result = false;
            try
            {
                Directory.Move(Local_DIR, Queue_DIR + "techsupport_DHCC");
                Logger.Info("Process Move to Queue completed");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }
        private static void get_DateRanges(string Days_str, string Date)
        {
            Logger.Info("Process DATE Ranges started");
            try
            {
                List<string> start_dates = new List<string>();
                List<string> end_dates = new List<string>();
                int Days;


                if (Days_str.Length > 0)
                {
                    Days = Convert.ToInt32(Days_str);
                }
                else { Days = 5; }

                DateTime start = DateTime.ParseExact(Date, "dd-MM-yyyy", null);
                //DateTime end = Convert.ToDateTime(txt_EndDate.Text);
                DateTime end = start.AddDays(Days);


                while (start < DateTime.Now)
                {
                    start_dates.Add(start.ToString("dd-MM-yyyy"));
                    end_dates.Add(end.ToString("dd-MM-yyyy"));

                    start = start.AddDays(Days + 1);
                    end = start.AddDays(Days);
                }
                global_start = start_dates.ToArray();
                global_end = end_dates.ToArray();
                Logger.Info("Process DATE Ranges completed");

            }

            catch (Exception ex)
            {
                Logger.Error(ex);
            }

        }
        private static void move_files(string file_absloute)
        {
            Logger.Info("Moving File " + file_absloute);
            try
            {
                //File.Move(file_absloute, Queue_dir + Path.GetFileName(file_absloute));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
        private static string ListtoStirng(string[] data)
        {
            string result = string.Empty;

            try
            {
                foreach (string datum in data)
                {
                    result = result + datum + "^";
                }

                result = result.Remove(result.Length - 1, 1);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return result;
        }


        //protected void chk_iterate_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (chk_iterate.Checked == true)
        //    {
        //        pnl_interval.Visible = true;
        //        pnl_toDate.Visible = false;
        //    }
        //    else
        //    {
        //        pnl_interval.Visible = false;
        //        pnl_toDate.Visible = true;
        //    }
        //}

        //public static void Insert_toDB(string query, string connection)
        //{
        //    connection = "Data Source=" + Connections.run_singlevalue("Automation", "server") + ";Initial Catalog=" + Connections.run_singlevalue("Automation", "database") + ";User ID=" + Connections.run_singlevalue("Automation", "username") + ";Password=" + Connections.run_singlevalue("Automation", "password");
        //    //connection = "Data Source=10.11.13.183 ;Initial Catalog=FS_WS_WSCTFW ;User ID=fshaikh ;Password=Dell@900 ;Connection Timeout=30;";
        //    Logger.Info("Inserting records to FS DB");
        //    try
        //    {
        //        using (SqlConnection con = new SqlConnection(connection))
        //        {
        //            using (SqlCommand command = new SqlCommand(query, con))
        //            {
        //                con.Open();
        //                if (command.ExecuteNonQuery() > 0)
        //                {
        //                    Logger.Info(command.ExecuteNonQuery() + " Records added successfully");
        //                }
        //            }
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Info("Insert To DB failed");
        //        Logger.Error(ex);
        //    }
        //}
        #endregion

    }

    public class GateParams
    {
        public string DHA_Input;

        public string LMU_isActive;
        public string LMU_Source;
        public string LMU_Status;

        public string Out_isActive;
        public string Out_Source;

    }
}
