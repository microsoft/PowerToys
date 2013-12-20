using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred
{
    public partial class FrmMain : Form
    {
        public List<IPlugin> plugins = new List<IPlugin>();
        private List<Result> results = new List<Result>();

        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
        }

        private void TbQuery_TextChanged(object sender, EventArgs e)
        {
            results.Clear();
            foreach (IPlugin plugin in plugins)
            {
                results.AddRange(plugin.Query(new Query(tbQuery.Text)));
            }
            var s = results.OrderByDescending(o => o.Score);
        }

    }
}
