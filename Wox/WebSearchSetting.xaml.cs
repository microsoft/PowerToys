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
    public partial class WebSearchSetting : Window
    {
        private SettingWidow settingWidow;
        private bool update;
        private WebSearch updateWebSearch;

        public WebSearchSetting(SettingWidow settingWidow)
        {
            this.settingWidow = settingWidow;
            InitializeComponent();
        }

        public void UpdateItem(WebSearch webSearch)
        {
            updateWebSearch = CommonStorage.Instance.UserSetting.WebSearches.FirstOrDefault(o => o == webSearch);
            if (updateWebSearch == null || string.IsNullOrEmpty(updateWebSearch.Url))
            {
                MessageBox.Show("Invalid web search");
                Close();
                return;
            }

            update = true;
            lblAdd.Text = "Update";
            cbEnable.IsChecked = webSearch.Enabled;
            tbTitle.Text = webSearch.Title;
            tbUrl.Text = webSearch.Url;
            tbActionword.Text = webSearch.ActionWord;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string title = tbTitle.Text;
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Please input Title field");
                return;
            }

            string url = tbUrl.Text;
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please input URL field");
                return;
            }

            string action = tbActionword.Text;
            if (string.IsNullOrEmpty(action))
            {
                MessageBox.Show("Please input ActionWord field");
                return;
            }


            if (!update)
            {
                if (CommonStorage.Instance.UserSetting.WebSearches.Exists(o => o.ActionWord == action))
                {
                    MessageBox.Show("ActionWord has existed, please input a new one.");
                    return;
                }
                CommonStorage.Instance.UserSetting.WebSearches.Add(new WebSearch()
                {
                    ActionWord = action,
                    Enabled = cbEnable.IsChecked ?? false,
                    IconPath = "",
                    Url = url,
                    Title = title
                });
                MessageBox.Show(string.Format("Add {0} web search successfully!", title));
            }
            else
            {
                updateWebSearch.ActionWord = action;
                updateWebSearch.IconPath = "";
                updateWebSearch.Enabled = cbEnable.IsChecked ?? false;
                updateWebSearch.Url = url;
                updateWebSearch.Title= title;
                MessageBox.Show(string.Format("Update {0} web search successfully!", title));
            }
            CommonStorage.Instance.Save();
            settingWidow.ReloadWebSearchView();
            Close();
        }
    }
}
