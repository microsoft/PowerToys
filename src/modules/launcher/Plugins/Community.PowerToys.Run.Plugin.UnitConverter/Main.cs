using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ManagedCommon;
using UnitsNet;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, IDisposable
    {
        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;
        private static readonly QuantityType[] _included = new QuantityType[]
        {
            QuantityType.Acceleration,
            QuantityType.Angle,
            QuantityType.Area,
            QuantityType.Duration,
            QuantityType.Energy,
            QuantityType.Information,
            QuantityType.Length,
            QuantityType.Mass,
            QuantityType.Power,
            QuantityType.Pressure,
            QuantityType.Speed,
            QuantityType.Temperature,
            QuantityType.Volume,
        };

        private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        private readonly int _roundingFractionalDigits = 4;

        public void Init(PluginInitContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(paramName: nameof(context));
            }

            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            string[] split = query.Search.Split(' ');

            InputInterpreter.ShorthandFeetInchHandler(ref split, _currentCulture);
            InputInterpreter.InputSpaceInserter(ref split);

            if (split.Length < 4 || split.Length > 4)
            {
                // deny any other queries than:
                // 10 ft in cm
                // 10 ft to cm
                return new List<Result>();
            }

            InputInterpreter.DegreePrefixer(ref split);

            List<Result> final_list = new List<Result>();

            foreach (QuantityType quantityType in _included)
            {
                double convertedValue = UnitHandler.ConvertInput(split, quantityType, _currentCulture);

                if (!double.IsNaN(convertedValue))
                {
                    UnitConversionResult result = new UnitConversionResult(Math.Round(convertedValue, _roundingFractionalDigits), split[3], quantityType);
                    AddToResult(final_list, result);
                }
            }

            return final_list;
        }

        private void AddToResult(List<Result> currentList, UnitConversionResult result)
        {
            // answer found, add result to list
            currentList.Add(new Result
            {
                ContextData = result,
                Title = string.Format("{0} {1}", result.ConvertedValue, result.UnitName),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = string.Format(Properties.Resources.copy_to_clipboard, result.QuantityType),
                Action = c =>
                {
                    var ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(result.ConvertedValue.ToString());
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
            });
        }

        private ContextMenuResult CreateContextMenuEntry(UnitConversionResult result)
        {
            return new ContextMenuResult
            {
                PluginName = Name,
                Title = Properties.Resources.context_menu_copy,
                Glyph = "\xE8C8",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                Action = _ =>
                {
                    bool ret = false;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            Clipboard.SetText(result.ConvertedValue.ToString());
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

        private void OnThemeChanged(Theme _, Theme newTheme)
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
                    _context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
