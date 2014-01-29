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
using System.Windows.Shapes;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;

namespace Wox
{
    public partial class WebSearchSetting : Window
    {
        public WebSearchSetting()
        {
            InitializeComponent();
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
            if (CommonStorage.Instance.UserSetting.WebSearches.Exists(o => o.ActionWord == action))
            {
                MessageBox.Show("ActionWord has existed, please input a new one.");
                return;
            }

            CommonStorage.Instance.UserSetting.WebSearches.Add(new WebSearch()
            {
                ActionWord =  action,
                Enabled = true,
                IconPath="",
                Url = url,
                Title = title
            });
            CommonStorage.Instance.Save();
            MessageBox.Show("Succeed!");
        }
    }
}
