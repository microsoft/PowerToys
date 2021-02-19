using ManagedCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using UnitsNet;
using Wox.Plugin;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class Main : IPlugin, /*IPluginI18n,*/ IDisposable
    {
        private PluginInitContext _context;
        private static string _icon_path;
        private bool _disposed;
        private readonly QuantityType[] _included = new QuantityType[] { QuantityType.Acceleration, QuantityType.Length, QuantityType.Mass, QuantityType.Speed, QuantityType.Temperature, QuantityType.Volume };
        private CultureInfo _currentCulture = CultureInfo.InvariantCulture;

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

            shorthandFeetInchHandler(ref split);
            degreePrefixer(ref split);

            if (split.Length < 4 || split.Length > 4) {
                // deny any other queries than:
                // 10 ft in cm
                // 10 ft to cm
                return new List<Result>();
            }

            string input_first_unit = split[1].ToLower();
            string input_second_unit = split[3].ToLower();
            double converted = -1;

            List<Result> final_list = new List<Result>();

            foreach (QuantityType quantity_type in _included) {
                QuantityInfo unit_info = Quantity.GetInfo(quantity_type);
                bool first_unit_is_abbreviated = UnitParser.Default.TryParse(split[1], unit_info.UnitType, out Enum first_unit);
                bool second_unit_is_abbreviated = UnitParser.Default.TryParse(split[3], unit_info.UnitType, out Enum second_unit);

                // 3 types of matches:
                // a) 10 ft in cm (double abbreviation)
                // b) 10 feet in centimeter (double unabbreviated)
                // c) 10 feet in cm (single abbreviation)

                if (first_unit_is_abbreviated && second_unit_is_abbreviated) {
                    // a
                    converted = UnitsNet.UnitConverter.ConvertByAbbreviation(double.Parse(split[0], _currentCulture), unit_info.Name, input_first_unit, input_second_unit);
                    AddToResult(final_list, converted, split[3]);
                }
                else if ((!first_unit_is_abbreviated) && (!second_unit_is_abbreviated)) {
                    // b
                    bool first_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_first_unit);
                    bool second_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_second_unit);

                    if (first_unabbreviated && second_unabbreviated) {
                        converted = UnitsNet.UnitConverter.ConvertByName(double.Parse(split[0], _currentCulture), unit_info.Name, input_first_unit, input_second_unit);
                        AddToResult(final_list, converted, split[3]);
                    }
                }
                else if ((first_unit_is_abbreviated && !second_unit_is_abbreviated) || (!first_unit_is_abbreviated && second_unit_is_abbreviated)) {
                    // c
                    if (first_unit_is_abbreviated) {
                        bool second_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_second_unit);

                        if (second_unabbreviated) {
                            UnitInfo second = Array.Find(unit_info.UnitInfos, info => info.Name.ToLower() == input_second_unit.ToLower());
                            converted = UnitsNet.UnitConverter.Convert(double.Parse(split[0], _currentCulture), first_unit, second.Value);
                            AddToResult(final_list, converted, split[3]);
                        }
                    }
                    else if (second_unit_is_abbreviated) {
                        bool first_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_first_unit);

                        if (first_unabbreviated) {
                            UnitInfo first = Array.Find(unit_info.UnitInfos, info => info.Name.ToLower() == input_first_unit.ToLower());
                            converted = UnitsNet.UnitConverter.Convert(double.Parse(split[0], _currentCulture), first.Value, second_unit);
                            AddToResult(final_list, converted, split[3]);
                        }
                    }
                }
            }

            return final_list;
        }

        /// <summary>
        /// Replaces a split input array with shorthand feet/inch notation (1', 1'2" etc) to 'x foot in cm'. 
        /// </summary>
        /// <param name="split"></param>
        private void shorthandFeetInchHandler(ref string[] split) {
            // catches 1' || 1" || 1'2 || 1'2" in cm
            // by converting it to "x foot in cm"
            if (split.Length == 3) {
                string[] shortsplit = Regex.Split(split[0], @"(?<=\d)(?![,.])(?=\D)|(?<=\D)(?<![,.])(?=\d)"); // todo ',' or '.' should depend on culture

                switch (shortsplit.Length) {
                    case 2:
                        // ex: 1' & 1"
                        if (shortsplit[1] == "\'") {
                            string[] newInput = new string[] { shortsplit[0], "foot", split[1], split[2] };
                            split = newInput;
                        }
                        else if (shortsplit[1] == "\"") {
                            string[] newInput = new string[] { shortsplit[0], "inch", split[1], split[2] };
                            split = newInput;
                        }
                        break;

                    case 3:
                    case 4:
                        // ex: 1'2 and 1'2"
                        if (shortsplit[1] == "\'") {
                            bool isFeet = double.TryParse(shortsplit[0], NumberStyles.AllowDecimalPoint, _currentCulture, out double feet);
                            bool isInches = double.TryParse(shortsplit[2], NumberStyles.AllowDecimalPoint, _currentCulture, out double inches);

                            if (!isFeet || !isInches) {
                                // one of either could not be parsed correctly
                                break;
                            }
                            
                            double totalInFeet = Length.FromFeetInches(feet, inches).Feet; 
                            string convertedTotalInFeet = totalInFeet.ToString();

                            if (_currentCulture == CultureInfo.InvariantCulture) {
                                // todo: actually make this work for more cultures where decimal parsing could break (e.g. '1,5' != 15)
                                convertedTotalInFeet = totalInFeet.ToString().Replace(',', '.');
                            }

                            string[] newInput = new string[] { convertedTotalInFeet, "foot", split[1], split[2] };
                            split = newInput;
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Adds degree prefixes to degree units for shorthand notation. E.g. '10 c in fahrenheit' to '10 °c in degreeFahrenheit'. 
        /// </summary>
        /// <param name="split"></param>
        private void degreePrefixer(ref string[] split) {
            switch (split[1]) {
                case "celsius":
                    split[1] = "degreeCelsius";
                    break;

                case "fahrenheit":
                    split[1] = "degreeFahrenheit";
                    break;

                case "c":
                    split[1] = "°c";
                    break;

                case "f":
                    split[1] = "°f";
                    break;

                default:
                    break;
            }

            switch (split[3]) {
                case "celsius":
                    split[3] = "degreeCelsius";
                    break;

                case "fahrenheit":
                    split[3] = "degreeFahrenheit";
                    break;

                case "c":
                    split[3] = "°c";
                    break;

                case "f":
                    split[3] = "°f";
                    break;

                default:
                    break;
            }


        }

        private void AddToResult(List<Result> currentList, double converted_value, string unit_name) {
            // answer found, add result to list
            currentList.Add(new Result {
                Title = string.Format("{0} {1}", converted_value, unit_name),
                IcoPath = _icon_path,
                Score = 300,
                SubTitle = "Copy to clipboard", //Context.API.GetTranslation("wox_plugin_calculator_copy_number_to_clipboard"),
                Action = c => {
                    var ret = false;
                    var thread = new Thread(() => {
                        try {
                            Clipboard.SetText(converted_value.ToString());
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
                }
            });
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
