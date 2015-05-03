using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private ProgramSource _editing;

        public AddProgramSource()
        {
            InitializeComponent();
        }

        public AddProgramSource(ProgramSource edit) : this()
        {
            this._editing = edit;
            this.Directory.Text = this._editing.Location;
            this.MaxDepth.Text = this._editing.MaxDepth.ToString();
            this.Suffixes.Text = this._editing.Suffixes;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.Directory.Text = dialog.SelectedPath;
            }
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            int max;
            if(!int.TryParse(this.MaxDepth.Text, out max))
            {
                max = -1;
            }

            if(this._editing == null)
            {
                ProgramStorage.Instance.ProgramSources.Add(new ProgramSource()
                {
                    Location = this.Directory.Text,
                    MaxDepth = max,
                    Suffixes = this.Suffixes.Text,
                    Type = "FileSystemProgramSource",
                    Enabled = true
                });
            }
            else
            {
                this._editing.Location = this.Directory.Text;
                this._editing.MaxDepth = max;
                this._editing.Suffixes = this.Suffixes.Text;
            }

            ProgramStorage.Instance.Save();
            this.DialogResult = true;
            this.Close();
        }
    }
}
