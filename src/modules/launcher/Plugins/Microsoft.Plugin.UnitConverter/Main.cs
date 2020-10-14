using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Microsoft.Plugin.UnitConverter
{
    public class Main : IPlugin, /*IPluginI18n,*/ IDisposable
    {
        private PluginInitContext _context;
        private static string _warningIconPath;
        private bool _disposed;

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

            System.Diagnostics.Debug.WriteLine(string.Format("works {0}", query));

            return new List<Result>();
        }

        private void OnThemeChanged(Theme _, Theme newTheme) {
            UpdateIconPath(newTheme);
        }

        private static void UpdateIconPath(Theme theme) {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite) {
                _warningIconPath = "Images/Warning.light.png";
            }
            else {
                _warningIconPath = "Images/Warning.dark.png";
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
