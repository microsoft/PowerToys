using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl
    {
        private PluginInitContext context;

        public ProgramSetting(PluginInitContext context)
        {
            this.context = context;
            InitializeComponent();
            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            programSourceView.ItemsSource = ProgramStorage.Instance.ProgramSources;
        }

        private void ReIndexing()
        {
            programSourceView.Items.Refresh();
            ThreadPool.QueueUserWorkItem(t =>
            {
                Dispatcher.Invoke(new Action(() => { indexingPanel.Visibility = Visibility.Visible; }));
                Programs.IndexPrograms();
                Dispatcher.Invoke(new Action(() => { indexingPanel.Visibility = Visibility.Hidden; }));
            });
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = folderBrowserDialog.SelectedPath;

                ProgramStorage.Instance.ProgramSources.Add(new ProgramSource()
                {
                    Location = path,
                    Type = "FileSystemProgramSource",
                    Enabled = true
                });
                ProgramStorage.Instance.Save();
                ReIndexing();
            }
        }

        private void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (selectedProgramSource != null)
            {
                string msg = string.Format(context.API.GetTranslation("wox_plugin_program_delete_program_source"), selectedProgramSource.Location);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    ProgramStorage.Instance.ProgramSources.Remove(selectedProgramSource);
                    ProgramStorage.Instance.Save();
                    ReIndexing();
                }
            }
            else
            {
                string msg = context.API.GetTranslation("wox_plugin_program_pls_select_program_source");
                MessageBox.Show(msg);
            }
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (selectedProgramSource != null)
            {
                //todo: update
                var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = folderBrowserDialog.SelectedPath;
                    selectedProgramSource.Location = path;
                    ProgramStorage.Instance.Save();
                    ReIndexing();
                }
            }
            else
            {
                string msg = context.API.GetTranslation("wox_plugin_program_pls_select_program_source");
                MessageBox.Show(msg);
            }
        }

        private void btnReindex_Click(object sender, RoutedEventArgs e)
        {
            ReIndexing();
        }

        private void BtnProgramSuffixes_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSuffixes p = new ProgramSuffixes(context);
            p.ShowDialog();
        }

        private void programSourceView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void programSourceView_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0)
            {
                foreach (string s in files)
                {
                    if (System.IO.Directory.Exists(s) == true)
                    {
                        ProgramStorage.Instance.ProgramSources.Add(new ProgramSource()
                        {
                            Location = s,
                            Type = "FileSystemProgramSource",
                            Enabled = true
                        });

                        ProgramStorage.Instance.Save();
                        ReIndexing();
                    }
                }
            }
        }
    }
}