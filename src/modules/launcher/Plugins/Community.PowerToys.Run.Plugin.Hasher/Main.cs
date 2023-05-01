// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Community.PowerToys.Run.Plugin.Hasher.Properties;
using ManagedCommon;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        public string Name => Resources.plugin_name;

        public string Description => Resources.plugin_description;

        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;

        public void Init(PluginInitContext context)
        {
            context = context ?? throw new ArgumentNullException(paramName: nameof(context));

            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private static void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _icon_path = "Images/hasher.light.png";
            }
            else
            {
                _icon_path = "Images/hasher.dark.png";
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_context != null && _context.API != null)
                    {
                        _context.API.ThemeChanged -= OnThemeChanged;
                    }

                    _disposed = true;
                }
            }
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.plugin_description;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.plugin_name;
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            HashRequest hashRequest = InputParser.RequestedHash(query.Search);

            var results = new List<Result>();
            if (hashRequest == null)
            {
                results = HashUtility.HashData(Encoding.UTF8.GetBytes(query.Search))
                .Select(x => GetResult(x))
                .ToList();
            }
            else
            {
                results.Add(GetResult(HashUtility.ComputeHashRequest(hashRequest)));
            }

            return results;
        }

        private Result GetResult(HashResult hashResult)
        {
            return new Result
            {
                ContextData = hashResult,
                Title = hashResult.ToString(null),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = Encoding.UTF8.GetString(hashResult.Content),
                Action = c =>
                {
                    var ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(hashResult.GetHashAsString());
                            ret = true;
                        }
                        catch (ExternalException)
                        {
                            MessageBox.Show(Properties.Resources.copy_failed);
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return ret;
                },
            };
        }
    }
}
