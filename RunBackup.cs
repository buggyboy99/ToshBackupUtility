using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RestSharp;
using System.Web;
using System.Net;
using System.IO;
using System.Xml;
using System.Net.Mail;

namespace ToshBackupUtility
{

/* 
 * File: RunBackup.cs
 * Author: Jake Maclean
 * Date: 30/05/2016
 * Purpose: Automatically download & email a Toshiba MFP clone file (.enc) 
 * via TopAcess portal given a valid reachable I.P Address.
 */

    public class RunBackup
    {
        private string cloneName;
        private string cloneFileName;
        private string sessionCookie;
        private string csrfpIdToken;
        private string finalPartMsg;

        public string programStatus = "";
        public string modelName;
        public string serialNum;

        private bool csrfpIdStatic = true;
        private bool outboundEmailFailed = false;
        private bool incompatiableModel = false;

        public int progressStatus = 0;
        

        public event EventHandler BackupStatusEvent;
        private RestClient client = new RestClient();
        private XmlDocument xmlDoc = new XmlDocument();
        
        // Queries used to preform user login and generation of clone file

        private string loginQuery = @"<DeviceInformationModel><GetValue><Authentication><UserCredential></UserCredential></Authentication></GetValue>
                <GetValue><Panel><DiagnosticMode><Mode_08><Code_8913></Code_8913></Mode_08></DiagnosticMode></Panel></GetValue><SetValue><Authentication>
                <UserCredential><userName>admin</userName><passwd>123456</passwd><ipaddress>101.184.123.77</ipaddress><DepartmentManagement isEnable='false'>
                <requireDepartment></requireDepartment></DepartmentManagement><domainName></domainName></UserCredential></Authentication></SetValue><Command>
                <Login><commandNode>Authentication/UserCredential</commandNode><Params><appName>TOPACCESS</appName></Params></Login></Command><SaveSessionInformation>
                <SessionInfo><Information><type>LoginPassword</type><data>123456</data></Information><Information><type>LoginUser</type><data>admin</data></Information>
                </SessionInfo></SaveSessionInformation></DeviceInformationModel>";

        private string cloneQuery = @"<DeviceInformationModel><SetValue><Cloning><Generate><SelectedCloneSettings><Security>true</Security>
                <DefaultSettings>true</DefaultSettings><UserManagement>true</UserManagement><NetworkPrintService>true</NetworkPrintService>
                <AddressBook>false</AddressBook><Combined>true</Combined></SelectedCloneSettings></Generate></Cloning></SetValue>
                <Command><GenerateCloneFile><commandNode>Cloning/Generate</commandNode><Params><password contentType='Value'></password>
                <cloneClient contentType='Value'>TopAccess</cloneClient></Params></GenerateCloneFile></Command></DeviceInformationModel>";

        private string cloneProgressQuery = @"<DeviceInformationModel><GetValue><Cloning><Generate><progressStatus></progressStatus>
                    <resultStatus></resultStatus></Generate></Cloning></GetValue></DeviceInformationModel>";

        private string cloneFinishedQuery = @"<DeviceInformationModel><GetValue><Cloning><FileInfo></FileInfo></Cloning></GetValue><Command>
                <GetMetaData><commandNode>Cloning/Apply</commandNode></GetMetaData></Command></DeviceInformationModel>";

        private string serialNumQuery = @"<DeviceInformationModel><GetValue><Controller><Software><Licenses><ScanEnabler/><PrintEnabler/>
                </Licenses></Software><Information/><Settings><AdminSystemSettings><EFiling><eFilingEnabled/></EFiling></AdminSystemSettings>
                <WebDataRetentionPeriod/></Settings></Controller></GetValue><GetValue><MFP><DeviceState></DeviceState><ErrorState></ErrorState>
                <Printer></Printer><Fax></Fax><ModelName></ModelName><System><PageMemory></PageMemory><MainMemory></MainMemory></System>
                </MFP></GetValue><GetValue><FileStorages><FileStorage selected='1'><name>SaveAsFile</name></FileStorage>
                <FileStorage selected='1'><name>FaxStorage</name></FileStorage></FileStorages></GetValue><GetValue><Network><Adapters>
                <Wire/><Wireless/></Adapters><Protocols><TCP-IP><hostName></hostName></TCP-IP></Protocols></Network></GetValue><SetValue>
                <FileStorages><FileStorage selected='1'><name>SaveAsFile</name></FileStorage><FileStorage selected='1'><name>FaxStorage</name>
                </FileStorage></FileStorages></SetValue><Command><GetPhysicalSpaceInfo><commandNode>FileStorages</commandNode>
                </GetPhysicalSpaceInfo></Command></DeviceInformationModel>";

        public RunBackup(string hostAddress)
        {
            // Set defaults
            client.Proxy = System.Net.HttpWebRequest.GetSystemWebProxy();
            client.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
            client.BaseUrl = new Uri("http://" + hostAddress);
            client.FollowRedirects = true;
            client.AddDefaultHeader("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:39.0) Gecko/20100101 Firefox/39.0");
        }

        public void RunRegularBackup()
        {
            if (GetSeshAndTokenDataFromWebpage())
            {
                if (GetMachineSerialNumFromWebpage())
                {
                    if (ValidateTopAccessLoginDetails())
                    {
                        // Begin clone file generation
                        GenerateCloneFile();

                        // Clone file generated and ready for dl
                        if (IsCloneFileIsReadyForDownload())
                        {
                            if (cloneName != null && cloneName != "")
                            {
                                DownloadCloneFile(cloneName);
                                SendEmailWithCloneFileAttached();

                                UpdateStatus("done");
                             
                                string successEmailMsg = "Additionally, an email containing the backup has also been sent to Toshiba.";

                                string unsuccesfulEmailMsg = "However, we were unable to automatically send a copy to Toshiba. " + Environment.NewLine + Environment.NewLine + "Please send this manually" + 
                                "to addr@email.com by attaching the locally saved file provided.";

                                if (outboundEmailFailed) finalPartMsg = unsuccesfulEmailMsg;
                                else finalPartMsg = successEmailMsg;

                                MessageBox.Show("Your machine backup file \"" + cloneFileName + "\" has been succesfully saved locally for your own copy: " + Environment.NewLine 
                                    + Environment.NewLine + "(" + Directory.GetCurrentDirectory() + ")" + Environment.NewLine + Environment.NewLine + finalPartMsg + Environment.NewLine + "" 
                                    + Environment.NewLine + Environment.NewLine + "Thank you.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("An unexpected error has occured. Please try again. ErrorCode: 1002",
                                "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show("An error occured while backing up clone data. Please try again.",
                                "Connection error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Unable to login with the default credentials (admin:123456). Please reset to default and try again.",
                            "Invalid Credentials", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    // Non-fatal error - serial number & model number won't be correct however
                    // MessageBox.Show("" + "", "Connection error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (incompatiableModel) {
                    MessageBox.Show("Incompatible machine model. This program will only allow backup of Toshiba Ebx devices.", "Error");
                }
                else {
                    MessageBox.Show("An unexpected error has occured. Please try again and be sure you are using a valid networked TopAccess Portal address." 
                        + Environment.NewLine + Environment.NewLine + "If you're contected to a VPN network, please confirm your internet explorer proxy settings are valid.",
                        "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool GetSeshAndTokenDataFromWebpage()
        {
            UpdateStatus("seshtoken");

            // CSRF Token and session extraction
            var seshAndTokenRequest = new RestRequest("/?MAIN=DEVICE", Method.GET);
          
            RestResponse resp = (RestResponse) client.Execute(seshAndTokenRequest);

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                
                // Valid TopAcess I.P Address
                if (resp.Content.Contains("Javascript is disabled in your browser.Please enable it to view TopAccess"))
                {
                   
                    // Unique CSRF Token is embedded in source code
                    if (resp.Content.Contains("csrfInsert("))
                    {
                        try
                        {
                            csrfpIdToken = resp.Content.Split(new[] { "csrfInsert(\"csrfpId\", \"" }, StringSplitOptions.None)[1].Split('"')[0].ToString();
                            sessionCookie = resp.Cookies[0].Value;
                        }
                        catch (Exception) { return false; }
                    }
                    else
                    {
                        // CSRF Token is NOT unique/embedded in source code and is equal to session value instead
                        csrfpIdStatic = false;
                        csrfpIdToken = resp.Cookies[0].Value;
                        sessionCookie = csrfpIdToken;
                    }
                    return true;
                }
                else incompatiableModel = true;
                return false;
            }
            else return false;
        }

        private bool GetMachineSerialNumFromWebpage()
        {
            UpdateStatus("serialnum");
            var serialNumRequest = new RestRequest("/contentwebserver", Method.POST);
            serialNumRequest.AddParameter("text/xml", serialNumQuery, ParameterType.RequestBody);

            serialNumRequest.AddCookie("Session", sessionCookie);
            serialNumRequest.AddHeader("csrfpId", csrfpIdToken);

            RestResponse serialNumResponse = (RestResponse) client.Execute(serialNumRequest);

            if (serialNumResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    serialNum = GetNodeFromXMLData(serialNumResponse.Content, "MFPSerialNumber");
                    modelName = GetNodeFromXMLData(serialNumResponse.Content, "ModelName");

                    if (serialNum == null || serialNum == "") serialNum = "UnknownSerial";
                    if (modelName == null || modelName == "") modelName = "UnknownModel";
                }

                catch (Exception) { return false; }

                return true;
            }
            else return false;
        }


        private bool ValidateTopAccessLoginDetails()
        {
            UpdateStatus("login");
            var loginRequest = new RestRequest("/contentwebserver", Method.POST);

            loginRequest.AddCookie("Session", sessionCookie);
            loginRequest.AddHeader("csrfpId", csrfpIdToken);

            loginRequest.AddParameter("text/xml", loginQuery, ParameterType.RequestBody);
            RestResponse loginResponse = (RestResponse) client.Execute(loginRequest);

            if (loginResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var loginStatus = GetNodeFromXMLData(loginResponse.Content, "statusOfOperation");

                    if (loginStatus == "STATUS_OK")
                    {
                        if (!csrfpIdStatic)
                        {
                            sessionCookie = loginResponse.Cookies[0].Value;
                            csrfpIdToken = sessionCookie;
                        }
                    }
                    else return false;
                }
                catch (Exception) { return false; }
                return true;
            }

            return false;
        }

        private void GenerateCloneFile()
        {
            UpdateStatus("generateclone");
            var cloneRequest = new RestRequest("/contentwebserver", Method.POST);

            cloneRequest.AddCookie("Session", sessionCookie);
            cloneRequest.AddParameter("text/xml", cloneQuery, ParameterType.RequestBody);

            client.AddDefaultHeader("csrfpId", csrfpIdToken);
            RestResponse cloneResponse = (RestResponse) client.Execute(cloneRequest);

            if (cloneResponse.StatusCode == HttpStatusCode.OK)
            {
                var cloneStatus = GetNodeFromXMLData(cloneResponse.Content, "statusOfOperation");

                if (cloneStatus == "STATUS_OK")
                {
                    int progressAmount = 0;

                    while (progressAmount < 100)
                    {
                        progressAmount = GetCloneFileProgress();

                        if (progressAmount == -1) {
                            throw new Exception("Unable to estabish connection with host. Check internet connection.");
                        }

                        System.Threading.Thread.Sleep(1000);
                    }
                }
                else
                {
                    MessageBox.Show("Unexpected error occured. Please try again. Error Code: 1005");
                }
            }
        }

        public int GetCloneFileProgress()
        {

            var cloneProgressRequest = new RestRequest("/contentwebserver", Method.POST);

            cloneProgressRequest.AddCookie("Session", sessionCookie);
            cloneProgressRequest.AddParameter("text/xml", cloneProgressQuery, ParameterType.RequestBody);

            RestResponse cloneProgressResponse = (RestResponse) client.Execute(cloneProgressRequest);

            if (cloneProgressResponse.StatusCode == HttpStatusCode.OK)
            {
                var responsePercentage = GetNodeFromXMLData(cloneProgressResponse.Content, "progressStatus");
                int finalPercentage;

                if (responsePercentage != null && responsePercentage != "")
                {
                    finalPercentage = Convert.ToInt32(responsePercentage);
                }
                else
                {
                    throw new Exception("An error occured while generating the clone file. Please try again. Error Code: 1007");
                }

                progressStatus = finalPercentage;

                UpdateStatus(null);

                return finalPercentage;
            }

            return -1;
        }

        private bool IsCloneFileIsReadyForDownload()
        {
            var cloneFinishedRequest = new RestRequest("/contentwebserver", Method.POST);

            cloneFinishedRequest.AddCookie("Session", sessionCookie);
            cloneFinishedRequest.AddParameter("text/xml", cloneFinishedQuery, ParameterType.RequestBody);

            RestResponse cloneFinishedResponse = (RestResponse) client.Execute(cloneFinishedRequest);

            var cloneFinishedStatus = GetNodeFromXMLData(cloneFinishedResponse.Content, "statusOfOperation");

            if (cloneFinishedResponse.StatusCode == HttpStatusCode.OK)
            {
                if (cloneFinishedStatus == "STATUS_OK")
                {
                    var cloneFileId = GetNodeFromXMLData(cloneFinishedResponse.Content, "filename");
                    var cloneCopierID = GetNodeFromXMLData(cloneFinishedResponse.Content, "CopierID");
                    var cloneDate = GetNodeFromXMLData(cloneFinishedResponse.Content, "CreationDate");

                    if (cloneFileId != "" && cloneFileId != null)
                    {
                        cloneName = cloneFileId;
                        cloneFileName = "CLONE_DATA_" + modelName + "_" + serialNum + ".enc";
                        return true;
                    }
                }
            }

            return false;
        }

        private void DownloadCloneFile(string fileName)
        {
            UpdateStatus("saveas");

            string cloneDownloadUrl = "/contentwebserver/download/CloneFileGenerate/" + fileName + "?FileType=CloneFile";

            using (var sw2 = new FileStream(cloneFileName, FileMode.Create))
            {
                var request = new RestRequest(cloneDownloadUrl, Method.GET);
                request.AddCookie("Session", sessionCookie);

                RestResponse resp2 = (RestResponse) client.Execute(request);

                sw2.Write(resp2.RawBytes, 0, resp2.RawBytes.Length);
            }
        }


        private string GetNodeFromXMLData(string xmlData, string node, int occurance = 0)
        {
            xmlDoc.LoadXml(xmlData);
            return xmlDoc.GetElementsByTagName(node)[occurance].InnerText;
        }

        private void UpdateStatus(string action)
        {
            programStatus = action;
            BackupStatusEvent(this, null);
        }

        private void SendEmailWithCloneFileAttached()
        {
            UpdateStatus("email");
            
            /*
            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
 
            mail.From = new MailAddress("youremail@domain.com");
            mail.To.Add("youremail@gmail.com");
            mail.Subject = "New clone file: " + cloneFileName;
            mail.Body = "";

            System.Net.Mail.Attachment attachment;
            attachment = new System.Net.Mail.Attachment(cloneFileName);
            mail.Attachments.Add(attachment);
            //SmtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;
            SmtpServer.Port = 587;
            SmtpServer.Credentials = new System.Net.NetworkCredential("youremail@domain.com", "yourpasword");
            SmtpServer.EnableSsl = true;
 
            try
            {
                SmtpServer.Send(mail);
            }
            catch (Exception)
            {
                outboundEmailFailed = true;
            }
             */
        }
    }
}
