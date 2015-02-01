using System.Windows;
using MarkdownSharp;
using Wox.Core.i18n;
using Wox.Core.Updater;

namespace Wox
{
    public partial class WoxUpdate : Window
    {
        public WoxUpdate()
        {
            InitializeComponent();

            string newVersionAvailable = string.Format(
                InternationalizationManager.Instance.GetTranslation("update_wox_update_new_version_available"),
                UpdaterManager.Instance.NewRelease);
            tbNewVersionAvailable.Text = newVersionAvailable;
            Markdown markdown = new Markdown();
            wbDetails.NavigateToString(markdown.Transform(UpdaterManager.Instance.NewRelease.description));
            lbUpdatedFiles.ItemsSource = UpdaterManager.Instance.GetAvailableUpdateFiles();
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdaterManager.Instance.ApplyUpdates();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            UpdaterManager.Instance.CleanUp();
            Close();
        }
    }
}
