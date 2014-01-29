using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Wox.Plugin.Doc
{
    public partial class DocViewFrm : Form
    {
        public DocViewFrm()
        {
            InitializeComponent();
            FormClosing+=DocViewFrm_FormClosing;
        }

        private void DocViewFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public void ShowDoc(string path)
        {
            //string html = File.ReadAllText(path);
            //webBrowser1.DocumentText = html;
            webBrowser1.Url = new Uri(String.Format("file:///{0}", path));
            Show();
        }
    }
}
