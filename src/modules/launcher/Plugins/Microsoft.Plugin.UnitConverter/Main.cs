using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using UnitConversion;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Microsoft.Plugin.UnitConverter
{
    public class Main : IPlugin, /*IPluginI18n,*/ IDisposable
    {
        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;
        private DistanceConverter _ft_to_m = new DistanceConverter("ft", "m");

        public void Init(PluginInitContext context) {
            if (context == null) {
                throw new ArgumentNullException(paramName: nameof(context));
            }

            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query) {
            if (query == null) {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            string[] split = query.Search.Split(' ');
            if (split.Length < 4) {
                return new List<Result>();
            }

            double ans = _ft_to_m.LeftToRight(double.Parse(split[0]));

            return new List<Result>        {                new Result {
                Title = string.Format("{0} {1}", ans.ToString(), split[3]),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = "Copy to clipboard", //Context.API.GetTranslation("wox_plugin_calculator_copy_number_to_clipboard"),
                Action = c => {
                    var ret = false;
                    var thread = new Thread(() => {
                        try {
                            Clipboard.SetText(ans.ToString());
                            ret = true;
                        }
                        catch (ExternalException) {
                            MessageBox.Show("Copy failed, please try later");
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return ret;
                },
            },};
        }

        private void OnThemeChanged(Theme _, Theme newTheme) {
            UpdateIconPath(newTheme);
        }

        private static void UpdateIconPath(Theme theme) {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite) {
                _icon_path = "Images/Warning.light.png";
            }
            else {
                _icon_path = "Images/Warning.dark.png";
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
