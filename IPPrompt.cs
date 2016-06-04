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
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Threading;

namespace ToshBackupUtility
{
    public partial class IPPrompt : Form
    {

/*
**********************************************************************************
* Start Button GUI
* Use previously validated input address to execute data backup process.
**********************************************************************************
*/
        private RunBackup rBackup;
        private RestClient restClient = new RestClient();
        private BackgroundWorker backWorker = new BackgroundWorker();
        private IPPrompt promp;

       
        private void startBtn_Click(object sender, EventArgs e)
        {
            backWorker.DoWork += new DoWorkEventHandler(StartBackup);
            backWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
           
            startBtn.Enabled = false;
            cancelBtn.Enabled = false;
            backWorker.RunWorkerAsync();
        }

        private void StartBackup(object sender, DoWorkEventArgs e)
        {
            rBackup = new RunBackup(textBox1.Text);
            rBackup.BackupStatusEvent += GetAndUpdateProgressStatusGUI;
            rBackup.RunRegularBackup();
        }

        public void GetAndUpdateProgressStatusGUI(object sender, EventArgs e)
        {
            try
            {
                progressBar1.GetCurrentParent().Invoke(new Action(() => progressBar1.Value = rBackup.progressStatus));
                statusLbl.GetCurrentParent().Invoke(new Action(() => statusLbl.Text = rBackup.progressStatus + " %"));
            }
            catch (Exception) { this.Close(); }

            switch (rBackup.programStatus)
            {
                case "seshtoken": statusLbl.Text = "Preparing to login"; break;
                case "serialnum": statusLbl.Text = "Preparing to login..."; break;
                case "login": statusLbl.Text = "Authenticating user credentials..."; break;
                case "generateclone": statusLbl.Text = "Generating clone file..."; break;
                case "done": statusLbl.Text = "Finished!"; System.Threading.Thread.Sleep(1000); break;
                case "saveas": statusLbl.Text = "Saving locally: " + Directory.GetCurrentDirectory(); break;
                case "email": statusLbl.Text = "Sending backup data to Toshiba..."; break;
            }
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
        }


/*
 * *********************************************************************************
 * Check Button GUI
 * Validate input I.P Address by pinging it and if required, 
 * verifying response status code if primary tests timed out (ICMP requests blocked).
 ***********************************************************************************
 */

        private void checkBtn_Click(object sender, EventArgs e)
        {
            string inputAddress = textBox1.Text;

            if (inputAddress != null && inputAddress != "")
            {
                try
                {
                    if (isWebAddressReachable(inputAddress) || isPingBlockedButReachable(inputAddress))
                    {
                        // Valid workable address
                        EnableValidIpFunctionality();
                    }
                    else
                    {
                        string errorMsg = "Unable to reach the ip address '" + inputAddress + @"'." + Environment.NewLine + Environment.NewLine + @"Please check to make sure it has been entered correctly. Also ensure the device is switched on and is accessible on the network." + Environment.NewLine;
                        MessageBox.Show(errorMsg, "Connection error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Invalid address provided. Please double check and try again.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                string errorMsg = "The I.P address field cannot be empty. Please try again.";
                
                MessageBox.Show(errorMsg, "Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool isWebAddressReachable(string addr)
        {
            System.Net.NetworkInformation.Ping p = new System.Net.NetworkInformation.Ping();
            PingReply reply = p.Send(textBox1.Text, 6000);

            if (reply.Status.ToString() == "Success") {
                return true; 
            }

            return false;
        }

        private bool isPingBlockedButReachable(string addr)
        {
            RestClient client2 = new RestClient();
            client2.Proxy = System.Net.HttpWebRequest.GetSystemWebProxy();
            client2.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            client2.BaseUrl = new Uri("http://" + textBox1.Text);
            client2.AddDefaultHeader("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:39.0) Gecko/20100101 Firefox/39.0");

            var request = new RestRequest("", Method.HEAD);
            request.Timeout = 6000;
            RestResponse resp2 = (RestResponse) client2.Execute(request);
  
            if (resp2.StatusCode == HttpStatusCode.OK) return true;

            return false;
        }

        private void EnableValidIpFunctionality()
        {
            textBox1.Enabled = false;
            checkBtn.Visible = false;
            checkBtn.Enabled = false;
            pictureBox1.Visible = true;
            startBtn.Enabled = true;
        }

        private void cancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        public IPPrompt()
        {
            promp = this;
            InitializeComponent();
        }
    }
}
