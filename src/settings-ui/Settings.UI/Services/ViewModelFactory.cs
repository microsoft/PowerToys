// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Factory service for creating PowerToys module ViewModels using reflection
    /// </summary>
    public class ViewModelFactory
    {
        private readonly ISettingsUtils _settingsUtils;
        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly Func<string, int> _sendConfigMSG;
        private readonly PowerLauncherSettings _powerLauncherSettings;

        // Static mapping of module names to ViewModel types
        private static readonly Dictionary<string, Type> _viewModelTypes = new()
        {
            [ModuleNames.AdvancedPaste] = typeof(AdvancedPasteViewModel),
            [ModuleNames.AlwaysOnTop] = typeof(AlwaysOnTopViewModel),
            [ModuleNames.ColorPicker] = typeof(ColorPickerViewModel),
            [ModuleNames.CmdPal] = typeof(CmdPalViewModel),
            [ModuleNames.CropAndLock] = typeof(CropAndLockViewModel),
            [ModuleNames.FancyZones] = typeof(FancyZonesViewModel),
            [ModuleNames.MeasureTool] = typeof(MeasureToolViewModel),
            [ModuleNames.MouseUtils] = typeof(MouseUtilsViewModel),
            [ModuleNames.MouseWithoutBorders] = typeof(MouseWithoutBordersViewModel),
            [ModuleNames.Peek] = typeof(PeekViewModel),
            [ModuleNames.PowerLauncher] = typeof(PowerLauncherViewModel),
            [ModuleNames.PowerOcr] = typeof(PowerOcrViewModel),
            [ModuleNames.TextExtractor] = typeof(PowerOcrViewModel),
            [ModuleNames.ShortcutGuide] = typeof(ShortcutGuideViewModel),
            [ModuleNames.Workspaces] = typeof(WorkspacesViewModel),
            [ModuleNames.ZoomIt] = typeof(ZoomItViewModel),
        };

        public ViewModelFactory(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> generalSettingsRepository,
            Func<string, int> sendConfigMSG)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _generalSettingsRepository = generalSettingsRepository ?? throw new ArgumentNullException(nameof(generalSettingsRepository));
            _sendConfigMSG = sendConfigMSG ?? throw new ArgumentNullException(nameof(sendConfigMSG));
            _powerLauncherSettings = SettingsRepository<PowerLauncherSettings>.GetInstance(_settingsUtils)?.SettingsConfig;
        }

        public PageViewModelBase CreateViewModel(string moduleKey)
        {
            if (!_viewModelTypes.TryGetValue(moduleKey, out var viewModelType))
            {
                return null;
            }

            try
            {
                return CreateViewModelInstance(viewModelType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating ViewModel for {moduleKey}: {ex.Message}");
                return null;
            }
        }

        private PageViewModelBase CreateViewModelInstance(Type viewModelType)
        {
            var constructors = viewModelType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = constructor.GetParameters();
                    var args = new object[parameters.Length];
                    bool canCreateInstance = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var paramName = parameters[i].Name?.ToLowerInvariant();

                        var arg = GetConstructorArgument(paramType, paramName, viewModelType);
                        if (arg == null && !paramType.IsValueType && !IsNullableType(paramType))
                        {
                            canCreateInstance = false;
                            break;
                        }

                        args[i] = arg;
                    }

                    if (canCreateInstance)
                    {
                        var instance = (PageViewModelBase)Activator.CreateInstance(viewModelType, args);
                        System.Diagnostics.Debug.WriteLine($"Successfully created {viewModelType.Name}");
                        return instance;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Constructor attempt failed for {viewModelType.Name}: {ex.Message}");
                    continue;
                }
            }

            return null;
        }

        private object GetConstructorArgument(Type paramType, string paramName, Type viewModelType)
        {
            // Standard dependency injection mappings
            if (paramType == typeof(ISettingsUtils))
            {
                return _settingsUtils;
            }

            if (paramType == typeof(ISettingsRepository<GeneralSettings>))
            {
                return _generalSettingsRepository;
            }

            if (paramType == typeof(Func<string, int>))
            {
                // Special case for PowerLauncher which uses ShellPage.SendDefaultIPCMessage
                if (viewModelType == typeof(PowerLauncherViewModel))
                {
                    var shellPageType = typeof(ShellPage);
                    var sendDefaultIPCMethod = shellPageType.GetMethod("SendDefaultIPCMessage", BindingFlags.Public | BindingFlags.Static);
                    if (sendDefaultIPCMethod != null)
                    {
                        return new Func<string, int>((string message) => (int)sendDefaultIPCMethod.Invoke(null, new object[] { message }));
                    }
                }

                return _sendConfigMSG;
            }

            if (paramType == typeof(PowerLauncherSettings))
            {
                return _powerLauncherSettings;
            }

            if (paramType == typeof(Microsoft.UI.Dispatching.DispatcherQueue))
            {
                return Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            }

            if (paramType == typeof(Func<bool>) && (paramName?.Contains("dark") == true || paramName?.Contains("theme") == true))
            {
                return new Func<bool>(() => App.IsDarkTheme());
            }

            if (paramType == typeof(bool) && (paramName?.Contains("dark") == true || paramName?.Contains("theme") == true))
            {
                return App.IsDarkTheme();
            }

            // Handle SettingsRepository<T> types
            if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ISettingsRepository<>))
            {
                var settingsType = paramType.GetGenericArguments()[0];
                var repositoryType = typeof(SettingsRepository<>).MakeGenericType(settingsType);
                var method = repositoryType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                return method?.Invoke(null, new object[] { _settingsUtils });
            }

            // Fallback values
            if (paramType == typeof(string))
            {
                return string.Empty;
            }

            if (paramType.IsValueType)
            {
                return Activator.CreateInstance(paramType);
            }

            if (IsNullableType(paramType))
            {
                return null;
            }

            return null;
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
