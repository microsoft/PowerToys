using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
using MessageBox = System.Windows.MessageBox;

namespace Wox
{
    public partial class ProgramSourceSetting : Window
    {
        private SettingWindow settingWindow;
        private bool update;
        private ProgramSource updateProgramSource;

        public ProgramSourceSetting(SettingWindow settingWidow)
        {
            this.settingWindow = settingWidow;
            InitializeComponent();

            this.cbType.ItemsSource = Wox.Plugin.System.Programs.SourceTypes.Select(o => o.Key).ToList();
        }

        public void UpdateItem(ProgramSource programSource)
        {
            updateProgramSource = CommonStorage.Instance.UserSetting.ProgramSources.FirstOrDefault(o => o == programSource);
            if (updateProgramSource == null)
            {
                MessageBox.Show("Invalid program source");
                Close();
                return;
            }

            update = true;
            lblAdd.Text = "Update";
            cbEnable.IsChecked = programSource.Enabled;
            cbType.SelectedItem = programSource.Type;
            cbType.IsEnabled = false;
            tbLocation.Text = programSource.Location;
            tbBonusPoints.Text = programSource.BonusPoints.ToString();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string location = tbLocation.Text;
            if (this.tbLocation.IsEnabled == true && string.IsNullOrEmpty(location))
            {
                MessageBox.Show("Please input Type field");
                return;
            }

            string type = cbType.SelectedItem as string;
            if (string.IsNullOrEmpty(type))
            {
                MessageBox.Show("Please input Type field");
                return;
            }

            int bonusPoint = 0;
            int.TryParse(this.tbBonusPoints.Text, out bonusPoint);

            if (!update)
            {
                ProgramSource p = new ProgramSource()
                {
                    Location = this.tbLocation.IsEnabled ? location : null,
                    Enabled = cbEnable.IsChecked ?? false,
                    Type = type,
                    BonusPoints = bonusPoint
                };
                if (CommonStorage.Instance.UserSetting.ProgramSources.Exists(o => o.ToString() == p.ToString() && o != p))
                {
                    MessageBox.Show("Program source already exists!");
                    return;
                }
                CommonStorage.Instance.UserSetting.ProgramSources.Add(p);
                MessageBox.Show(string.Format("Add {0} program source successfully!", p.ToString()));
            }
            else
            {
                if (CommonStorage.Instance.UserSetting.ProgramSources.Exists(o => o.ToString() == updateProgramSource.ToString() && o != updateProgramSource))
                {
                    MessageBox.Show("Program source already exists!");
                    return;
                }
                updateProgramSource.Location = this.tbLocation.IsEnabled ? location : null;
                updateProgramSource.Type = type;
                updateProgramSource.Enabled = cbEnable.IsChecked ?? false;
                updateProgramSource.BonusPoints = bonusPoint;
                MessageBox.Show(string.Format("Update {0} program source successfully!", updateProgramSource.ToString()));
            }
            CommonStorage.Instance.Save();
            settingWindow.ReloadProgramSourceView();
            Close();
        }

        private void cbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string item = cbType.SelectedItem as String;
            Type type;
            if (item != null && Wox.Plugin.System.Programs.SourceTypes.TryGetValue(item, out type))
            {
                var attrs = type.GetCustomAttributes(typeof(System.ComponentModel.BrowsableAttribute), false);
                if (attrs.Length > 0 && (attrs[0] as System.ComponentModel.BrowsableAttribute).Browsable == false)
                {
                    this.tbLocation.IsEnabled = false;
                    return;
                }
            }
            this.tbLocation.IsEnabled = true;
        }
    }
}
