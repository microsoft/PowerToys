using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Plugin.Program.Views.Models;
using Wox.Plugin.Program.Views.Commands;
using Wox.Plugin.Program.Programs;
using System.ComponentModel;
using System.Windows.Data;

namespace Wox.Plugin.Program.Views
{
    /// <summary>
    /// Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl
    {
        private PluginInitContext context;
        private Settings _settings;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection;

        internal static List<ProgramSource> ProgramSettingDisplayList { get; set; }

        public ProgramSetting(PluginInitContext context, Settings settings, Win32[] win32s, UWP.Application[] uwps)
        {
            this.context = context;
            InitializeComponent();
            Loaded += Setting_Loaded;
            _settings = settings;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            ProgramSettingDisplayList = _settings.ProgramSources.LoadProgramSources();
            programSourceView.ItemsSource = ProgramSettingDisplayList;

            StartMenuEnabled.IsChecked = _settings.EnableStartMenuSource;
            RegistryEnabled.IsChecked = _settings.EnableRegistrySource;
        }

        private void ReIndexing()
        {
            programSourceView.Items.Refresh();
            Task.Run(() =>
            {
                Dispatcher.Invoke(() => { indexingPanel.Visibility = Visibility.Visible; });
                Main.IndexPrograms();
                Dispatcher.Invoke(() => { indexingPanel.Visibility = Visibility.Hidden; });
            });
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var add = new AddProgramSource(context, _settings);
            if(add.ShowDialog() ?? false)
            {
                ReIndexing();
            }

            programSourceView.Items.Refresh();
        }

        private void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedProgramSource = programSourceView.SelectedItem as Settings.ProgramSource;
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
            var selectedProgramSource = programSourceView.SelectedItem as Settings.ProgramSource;
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
                        _settings.ProgramSources.Add(new Settings.ProgramSource
                        {
                            Location = s
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

        private void btnLoadAllProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSettingDisplayList.LoadAllApplications();

            programSourceView.Items.Refresh();
        }

        private void btnProgramSourceStatus_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = programSourceView
                                .SelectedItems.Cast<ProgramSource>()
                                .ToList();

            if (IsSelectedRowStatusEnabledMoreOrEqualThanDisabled())
            {
                ProgramSettingDisplayList.SetProgramSourcesStatus(selectedItems, false);

                ProgramSettingDisplayList.StoreDisabledInSettings();
            }
            else
            {
                ProgramSettingDisplayList.SetProgramSourcesStatus(selectedItems, true);

                ProgramSettingDisplayList.RemoveDisabledFromSettings();
            }            
            
            if (selectedItems.IsReindexRequired())
                ReIndexing();

            programSourceView.SelectedItems.Clear();

            programSourceView.Items.Refresh();
        }

        private void ProgramSourceView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            programSourceView.SelectedItems.Clear();
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);
                    
                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(programSourceView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private bool IsSelectedRowStatusEnabledMoreOrEqualThanDisabled()
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            return selectedItems.Where(x => x.Enabled).Count() >= selectedItems.Where(x => !x.Enabled).Count();
        }

        private void Row_Click(object sender, RoutedEventArgs e)
        {
            if (IsSelectedRowStatusEnabledMoreOrEqualThanDisabled())
            {
                btnProgramSourceStatus.Content = "Disable";
            }
            else
            {
                btnProgramSourceStatus.Content = "Enable";
            }
        }
    }
}