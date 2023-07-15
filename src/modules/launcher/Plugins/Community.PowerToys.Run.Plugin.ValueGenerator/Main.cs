// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Community.PowerToys.Run.Plugin.ValueGenerator.Properties;
using ManagedCommon;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        public string Name => Resources.plugin_name;

        public string Description => Resources.plugin_description;

        private PluginInitContext _context;
        private static bool _isLightTheme = true;
        private bool _disposed;
        private static InputParser _inputParser = new InputParser();

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
                _isLightTheme = true;
            }
            else
            {
                _isLightTheme = false;
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

            var results = new List<Result>();
            try
            {
                IComputeRequest computeRequest = _inputParser.ParseInput(query);
                results.Add(GetResult(computeRequest));
            }
            catch (ArgumentException e)
            {
                results.Add(GetErrorResult(e.Message));
            }
            catch (FormatException e)
            {
               Log.Debug(GetTranslatedPluginTitle() + ": " + e.Message, GetType());
            }

            return results;
        }

        private Result GetResult(IComputeRequest request)
        {
            request.Compute();

            return new Result
            {
                ContextData = request.Result,
                Title = request.ResultToString(),
                IcoPath = _isLightTheme ? "Images/ValueGenerator.dark.png" : "Images/ValueGenerator.light.png",
                Score = 300,
                SubTitle = request.Description,
                Action = c =>
                {
                    var ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(request.ResultToString());
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

        private Result GetErrorResult(string errorMessage)
        {
            return new Result
            {
                Title = Resources.error_title,
                SubTitle = errorMessage,
                IcoPath = _isLightTheme ? "Images/Warning.light.png" : "Images/Warning.dark.png",
                Action = _ => { return true; },
            };
        }
    }
}
