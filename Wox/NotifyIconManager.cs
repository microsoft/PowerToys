using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Wox.Core.Resource;
using Wox.Plugin;

namespace Wox
{
    public class NotifyIconManager
    {

        private NotifyIcon notifyIcon;
        private IPublicAPI _api;

        public NotifyIconManager(IPublicAPI api)
        {
            this.InitialTray();
            this._api = api;
        }

        private void InitialTray()
        {
            notifyIcon = new NotifyIcon { Text = "Wox", Icon = Properties.Resources.app, Visible = true };
            notifyIcon.Click += (o, e) => this._api.ShowApp();
            var open = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayOpen"));
            open.Click += (o, e) => this._api.ShowApp();
            var setting = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTraySettings"));
            setting.Click += (o, e) => this._api.OpenSettingDialog();
            var about = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayAbout"));
            about.Click += (o, e) => this._api.OpenSettingDialog("about");
            var exit = new MenuItem(InternationalizationManager.Instance.GetTranslation("iconTrayExit"));
            exit.Click += (o, e) => this._api.CloseApp();
            MenuItem[] childen = { open, setting, about, exit };
            notifyIcon.ContextMenu = new ContextMenu(childen);
        }
    }
}
