using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred
{
    public partial class FrmMain : Form
    {
        public List<PluginPair> plugins = new List<PluginPair>();
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
            foreach (PluginPair pair in plugins)
            {
                Query q = new Query(tbQuery.Text);
                if (pair.Metadata.ActionKeyword == q.ActionName)
                {
                    try
                    {
                        results.AddRange(pair.Plugin.Query(q));
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", pair.Metadata.Name, queryException.Message));
#if (DEBUG)
                        {
                            throw;
                        }
#endif
                        throw;
                    }
                }
            }
            var s = results.OrderByDescending(o => o.Score);

            listBox1.Items.Clear();
            foreach (Result result in results)
            {
                listBox1.Items.Add(result.Title);
            }
        }

    }
}
