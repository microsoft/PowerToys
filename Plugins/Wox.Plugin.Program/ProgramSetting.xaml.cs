using System.IO;
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
        private Settings _settings;

        public ProgramSetting(PluginInitContext context, Settings settings)
        {
            this.context = context;
            InitializeComponent();
            Loaded += Setting_Loaded;
            _settings = settings;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            programSourceView.ItemsSource = _settings.ProgramSources;
            StartMenuEnabled.IsChecked = _settings.EnableStartMenuSource;
            RegistryEnabled.IsChecked = _settings.EnableRegistrySource;
        }

        private void ReIndexing()
        {
            programSourceView.Items.Refresh();
            ThreadPool.QueueUserWorkItem(t =>
            {
                Dispatcher.Invoke(() => { indexingPanel.Visibility = Visibility.Visible; });
                Programs.IndexPrograms();
                Dispatcher.Invoke(() => { indexingPanel.Visibility = Visibility.Hidden; });
            });
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var add = new AddProgramSource(_settings);
            if(add.ShowDialog() ?? false)
            {
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
                    _settings.ProgramSources.Remove(selectedProgramSource);
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
                var add = new AddProgramSource(selectedProgramSource, _settings);
                if (add.ShowDialog() ?? false)
                {
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
            ProgramSuffixes p = new ProgramSuffixes(context, _settings);
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
                    if (Directory.Exists(s))
                    {
                        _settings.ProgramSources.Add(new ProgramSource
                        {
                            Location = s,
                            Type = "FileSystemProgramSource",
                            Enabled = true
                        });

                        ReIndexing();
                    }
                }
            }
        }

        private void StartMenuEnabled_Click(object sender, RoutedEventArgs e)
        {
            _settings.EnableStartMenuSource = StartMenuEnabled.IsChecked ?? false;
            ReIndexing();
        }

        private void RegistryEnabled_Click(object sender, RoutedEventArgs e)
        {
            _settings.EnableRegistrySource = RegistryEnabled.IsChecked ?? false;
            ReIndexing();
        }
    }
}