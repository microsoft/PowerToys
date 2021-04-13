using ManagedCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using UnitsNet;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        public string Name => Properties.Resources.plugin_name;
        public string Description => Properties.Resources.plugin_description;

        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;
        private static readonly QuantityType[] _included = new QuantityType[] { QuantityType.Acceleration, QuantityType.Length, QuantityType.Mass, QuantityType.Speed, QuantityType.Temperature, QuantityType.Volume };
        private CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        private int _roundingFractionalDigits = 4;

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
            split = Array.ConvertAll(split, x => x.ToLower());

            InputInterpreter.ShorthandFeetInchHandler(ref split, _currentCulture);
            InputInterpreter.DegreePrefixer(ref split);

            if (split.Length < 4 || split.Length > 4) {
                // deny any other queries than:
                // 10 ft in cm
                // 10 ft to cm
                return new List<Result>();
            }

            List<Result> final_list = new List<Result>();

            foreach (QuantityType quantityType in _included) {
                double convertedValue = UnitHandler.ConvertInput(split, quantityType, _currentCulture);
                if (!double.IsNaN(convertedValue)) {
                    AddToResult(final_list, convertedValue, split[3]);
                }
            }

            return final_list;
        }

        private void AddToResult(List<Result> currentList, double converted_value, string unit_name) {
            // answer found, add result to list
            currentList.Add(new Result {
                Title = string.Format("{0} {1}", Math.Round(converted_value, _roundingFractionalDigits), unit_name),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = Properties.Resources.copy_to_clipboard,
                Action = c => {
                    var ret = false;
                    var thread = new Thread(() => {
                        try {
                            Clipboard.SetText(converted_value.ToString());
                            ret = true;
                        }
                        catch (ExternalException) {
                            MessageBox.Show(Properties.Resources.copy_failed);
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return ret;
                }
            });
        }

        public string GetTranslatedPluginTitle() {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription() {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme _, Theme newTheme) {
            UpdateIconPath(newTheme);
        }

        private static void UpdateIconPath(Theme theme) {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite) {
                _icon_path = "Images/calculator.light.png";
            }
            else {
                _icon_path = "Images/calculator.dark.png";
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
