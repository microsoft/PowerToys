﻿// Copyright (c) Microsoft Corporation
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
using System.Windows.Input;

using ManagedCommon;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, IDisposable
    {
        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        public static string PluginID => "aa0ee9daff654fb7be452c2d77c471b9";

        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;

        private static readonly CompositeFormat CopyToClipboard = System.Text.CompositeFormat.Parse(Properties.Resources.copy_to_clipboard);

        public void Init(PluginInitContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            // Parse
            ConvertModel convertModel = InputInterpreter.Parse(query);
            if (convertModel == null)
            {
                return new List<Result>();
            }

            return UnitHandler.Convert(convertModel)
                .Select(x => GetResult(x))
                .ToList();
        }

        private Result GetResult(UnitConversionResult result)
        {
            return new Result
            {
                ContextData = result,
                Title = result.ToString(null),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = string.Format(CultureInfo.CurrentCulture, CopyToClipboard, result.QuantityInfo.Name),
                Action = c =>
                {
                    var ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(result.ConvertedValue.ToString(UnitConversionResult.CopyFormat, CultureInfo.CurrentCulture));
                            ret = true;
                        }
                        catch (ExternalException ex)
                        {
                            Log.Exception("Copy failed", ex, GetType());
                            MessageBox.Show(ex.Message, Properties.Resources.copy_failed);
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return ret;
                },
            };
        }

        private ContextMenuResult CreateContextMenuEntry(UnitConversionResult result)
        {
            return new ContextMenuResult
            {
                PluginName = Name,
                Title = Properties.Resources.context_menu_copy,
                Glyph = "\xE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                Action = _ =>
                {
                    bool ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(result.ConvertedValue.ToString(UnitConversionResult.CopyFormat, CultureInfo.CurrentCulture));
                            ret = true;
                        }
                        catch (ExternalException ex)
                        {
                            Log.Exception("Copy failed", ex, GetType());
                            MessageBox.Show(ex.Message, Properties.Resources.copy_failed);
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return ret;
                },
            };
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is UnitConversionResult))
            {
                return new List<ContextMenuResult>();
            }

            List<ContextMenuResult> contextResults = new List<ContextMenuResult>();
            UnitConversionResult result = selectedResult.ContextData as UnitConversionResult;
            contextResults.Add(CreateContextMenuEntry(result));

            return contextResults;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private static void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _icon_path = "Images/unitconverter.light.png";
            }
            else
            {
                _icon_path = "Images/unitconverter.dark.png";
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
    }
}
