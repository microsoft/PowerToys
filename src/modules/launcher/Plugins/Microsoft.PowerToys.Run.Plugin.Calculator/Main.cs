// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.Calculator.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public class Main : IPlugin, IPluginI18n, IDisposable, ISettingProvider
    {
        private const string InputUseEnglishFormat = nameof(InputUseEnglishFormat);
        private const string OutputUseEnglishFormat = nameof(OutputUseEnglishFormat);
        private const string ReplaceInput = nameof(ReplaceInput);

        private static readonly CalculateEngine CalculateEngine = new CalculateEngine();

        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        private bool _inputUseEnglishFormat;
        private bool _outputUseEnglishFormat;
        private bool _replaceInput;

        public string Name => Resources.wox_plugin_calculator_plugin_name;

        public string Description => Resources.wox_plugin_calculator_plugin_description;

        public static string PluginID => "CEA0FDFC6D3B4085823D60DC76F28855";

        private bool _disposed;

        private static readonly CompositeFormat WoxPluginCalculatorInEnFormatDescription = System.Text.CompositeFormat.Parse(Properties.Resources.wox_plugin_calculator_in_en_format_description);
        private static readonly CompositeFormat WoxPluginCalculatorOutEnFormatDescription = System.Text.CompositeFormat.Parse(Properties.Resources.wox_plugin_calculator_out_en_format_description);

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            // The number examples has to be created at runtime to prevent translation.
            new PluginAdditionalOption
            {
                Key = InputUseEnglishFormat,
                DisplayLabel = Resources.wox_plugin_calculator_in_en_format,
                DisplayDescription = string.Format(CultureInfo.CurrentCulture, WoxPluginCalculatorInEnFormatDescription, 1000.55.ToString("N2", new CultureInfo("en-us"))),
                Value = false,
            },
            new PluginAdditionalOption
            {
                Key = OutputUseEnglishFormat,
                DisplayLabel = Resources.wox_plugin_calculator_out_en_format,
                DisplayDescription = string.Format(CultureInfo.CurrentCulture, WoxPluginCalculatorOutEnFormatDescription, 1000.55.ToString("G", new CultureInfo("en-us"))),
                Value = false,
            },
            new PluginAdditionalOption
            {
                Key = ReplaceInput,
                DisplayLabel = Resources.wox_plugin_calculator_replace_input,
                DisplayDescription = Resources.wox_plugin_calculator_replace_input_description,
                Value = true,
            },
        };

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            bool isGlobalQuery = string.IsNullOrEmpty(query.ActionKeyword);
            bool replaceInput = _replaceInput && !isGlobalQuery && query.Search.EndsWith('=');
            CultureInfo inputCulture = _inputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;
            CultureInfo outputCulture = _outputUseEnglishFormat ? new CultureInfo("en-us") : CultureInfo.CurrentCulture;

            // Happens if the user has only typed the action key so far
            if (string.IsNullOrEmpty(query.Search))
            {
                return new List<Result>();
            }

            NumberTranslator translator = NumberTranslator.Create(inputCulture, new CultureInfo("en-US"));
            var input = translator.Translate(query.Search.Normalize(NormalizationForm.FormKC));

            if (replaceInput)
            {
                input = input[..^1];
            }

            if (!CalculateHelper.InputValid(input))
            {
                return new List<Result>();
            }

            try
            {
                // Using CurrentUICulture since this is user facing
                var result = CalculateEngine.Interpret(input, outputCulture, out string errorMessage);

                // This could happen for some incorrect queries, like pi(2)
                if (result.Equals(default(CalculateResult)))
                {
                    // If errorMessage is not default then do error handling
                    return errorMessage == default ? new List<Result>() : ErrorHandler.OnError(IconPath, isGlobalQuery, query.RawQuery, errorMessage);
                }
                else if (replaceInput)
                {
                    var pluginResult = ResultHelper.CreateResult(result.RoundedResult, IconPath, inputCulture, outputCulture);
                    Context.API.ChangeQuery($"{query.ActionKeyword} {pluginResult.QueryTextDisplay}");
                    return new List<Result>();
                }

                return new List<Result>
                {
                    ResultHelper.CreateResult(result.RoundedResult, IconPath, inputCulture, outputCulture),
                };
            }
            catch (Mages.Core.ParseException)
            {
                // Invalid input
                return ErrorHandler.OnError(IconPath, isGlobalQuery, query.RawQuery, Properties.Resources.wox_plugin_calculator_expression_not_complete);
            }
            catch (OverflowException)
            {
                // Result to big to convert to decimal
                return ErrorHandler.OnError(IconPath, isGlobalQuery, query.RawQuery, Properties.Resources.wox_plugin_calculator_not_covert_to_decimal);
            }
            catch (Exception e)
            {
                // Any other crash occurred
                // We want to keep the process alive if any the mages library throws any exceptions.
                return ErrorHandler.OnError(IconPath, isGlobalQuery, query.RawQuery, default, e);
            }
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(paramName: nameof(context));

            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/calculator.light.png";
            }
            else
            {
                IconPath = "Images/calculator.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.wox_plugin_calculator_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.wox_plugin_calculator_plugin_description;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var inputUseEnglishFormat = false;
            var outputUseEnglishFormat = false;
            var replaceInput = true;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var optionInputEn = settings.AdditionalOptions.FirstOrDefault(x => x.Key == InputUseEnglishFormat);
                inputUseEnglishFormat = optionInputEn?.Value ?? inputUseEnglishFormat;

                var optionOutputEn = settings.AdditionalOptions.FirstOrDefault(x => x.Key == OutputUseEnglishFormat);
                outputUseEnglishFormat = optionOutputEn?.Value ?? outputUseEnglishFormat;

                var optionReplaceInput = settings.AdditionalOptions.FirstOrDefault(x => x.Key == ReplaceInput);
                replaceInput = optionReplaceInput?.Value ?? replaceInput;
            }

            _inputUseEnglishFormat = inputUseEnglishFormat;
            _outputUseEnglishFormat = outputUseEnglishFormat;
            _replaceInput = replaceInput;
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
                    if (Context != null && Context.API != null)
                    {
                        Context.API.ThemeChanged -= OnThemeChanged;
                    }

                    _disposed = true;
                }
            }
        }
    }
}
