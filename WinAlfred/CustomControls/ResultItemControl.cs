using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WinAlfred.Plugin;

namespace WinAlfreds.CustomControls
{
    public partial class ResultItemControl : UserControl
    {
        public ResultItemControl(Result result)
        {
            InitializeComponent();

            lblTitle.Text = result.Title;
            lblSubTitle.Text = result.SubTitle;
        }
    }
}
