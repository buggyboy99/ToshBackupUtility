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

namespace ToshBackupUtility
{
    public partial class Form1 : Form
    {
        IPPrompt ipPrompt;

        public Form1()
        {
            InitializeComponent();
            ipPrompt = new IPPrompt();
        }

        // WORKING IPS: 160.94.52.106 | 128.46.81.40 | 35.9.85.120 | 149.84.148.245 | 149.84.150.5

        private void button1_Click(object sender, EventArgs e)
        {
            if (ipPrompt.IsDisposed)
            {
                ipPrompt = new IPPrompt();
            }

            if (!ipPrompt.Visible)
            {
                ipPrompt.Show();
            }
            
        }
    }
}
