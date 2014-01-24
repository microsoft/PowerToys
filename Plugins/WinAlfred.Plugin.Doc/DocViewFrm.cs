using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinAlfred.Plugin.Doc
{
    public partial class DocViewFrm : Form
    {
        public DocViewFrm()
        {
            InitializeComponent();
        }

        public void ShowDoc(string path)
        {
            webBrowser1.Url = new Uri(String.Format("file:///{0}", path));
            Show();
        }
    }
}
