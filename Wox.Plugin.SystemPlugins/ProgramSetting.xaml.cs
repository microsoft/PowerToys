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
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins
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
            ProgramSource seletedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (seletedProgramSource != null &&
                MessageBox.Show("Are your sure to delete " + seletedProgramSource.ToString(), "Delete ProgramSource",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserSettingStorage.Instance.ProgramSources.Remove(seletedProgramSource);
                programSourceView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            ProgramSource seletedProgramSource = programSourceView.SelectedItem as ProgramSource;
            if (seletedProgramSource != null)
            {
                ProgramSourceSetting programSource = new ProgramSourceSetting(this);
                programSource.UpdateItem(seletedProgramSource);
                programSource.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a program source");
            }
        }

    }
}
