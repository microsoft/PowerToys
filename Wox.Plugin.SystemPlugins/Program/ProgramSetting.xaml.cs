using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program
{
    /// <summary>
    /// Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl
    {
        public ProgramSetting()
        {
            InitializeComponent();
            Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            programSourceView.ItemsSource = UserSettingStorage.Instance.ProgramSources;
        }

        public void ReloadProgramSourceView()
        {
            programSourceView.Items.Refresh();
        }


        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSourceSetting programSource = new ProgramSourceSetting(this);
            programSource.ShowDialog();
        }

        private void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (selectedProgramSource != null)
            {
                if (MessageBox.Show("Are your sure to delete " + selectedProgramSource.ToString(), "Delete ProgramSource",
                     MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UserSettingStorage.Instance.ProgramSources.Remove(selectedProgramSource);
                    programSourceView.Items.Refresh();
                }
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (selectedProgramSource != null)
            {
                ProgramSourceSetting programSource = new ProgramSourceSetting(this);
                programSource.UpdateItem(selectedProgramSource);
                programSource.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
        }

    }
}
