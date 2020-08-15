// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Plugin.Program.Logger;
using Microsoft.Plugin.Program.Win32;
using Wox.Infrastructure;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
using Wox.Plugin.SharedCommands;
using static Microsoft.Plugin.Program.Programs.UWP;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public class UWPApplication : IProgram
    {
        public string AppListEntry { get; set; }

        public string UniqueIdentifier { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string UserModelId { get; set; }

        public string BackgroundColor { get; set; }

        public string EntryPoint { get; set; }

        public string Name => DisplayName;

        public string Location => Package.Location;

        public bool Enabled { get; set; }

        public bool CanRunElevated { get; set; }

        public string LogoPath { get; set; }

        public UWP Package { get; set; }

        private string logoUri;

        // Function to calculate the score of a result
        private int Score(string query)
        {
            var displayNameMatch = StringMatcher.FuzzySearch(query, DisplayName);
            var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
            var score = new[] { displayNameMatch.Score, descriptionMatch.Score / 2 }.Max();
            return score;
        }

        // Function to set the subtitle based on the Type of application
        private static string SetSubtitle(IPublicAPI api)
        {
            return api.GetTranslation("powertoys_run_plugin_program_packaged_application");
        }

        public Result Result(string query, IPublicAPI api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            var score = Score(query);
            if (score <= 0)
            { // no need to create result if score is 0
                return null;
            }

            var result = new Result
            {
                SubTitle = SetSubtitle(api),
                Icon = Logo,
                Score = score,
                ContextData = this,
                Action = e =>
                {
                    Launch(api);
                    return true;
                },
            };

            // To set the title to always be the displayname of the packaged application
            result.Title = DisplayName;
            result.TitleHighlightData = StringMatcher.FuzzySearch(query, Name).MatchData;

            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", api.GetTranslation("powertoys_run_plugin_program_file_name"), result.Title);
            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", api.GetTranslation("powertoys_run_plugin_program_file_path"), Package.Location);
            result.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentially keeping the process alive.")]
        public List<ContextMenuResult> ContextMenus(IPublicAPI api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            var contextMenus = new List<ContextMenuResult>();

            if (CanRunElevated)
            {
                contextMenus.Add(
                        new ContextMenuResult
                        {
                            PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                            Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                            Glyph = "\xE7EF",
                            FontFamily = "Segoe MDL2 Assets",
                            AcceleratorKey = Key.Enter,
                            AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                            Action = _ =>
                            {
                                string command = "shell:AppsFolder\\" + UniqueIdentifier;
                                command = Environment.ExpandEnvironmentVariables(command.Trim());

                                var info = ShellCommand.SetProcessStartInfo(command, verb: "runas");
                                info.UseShellExecute = true;

                                Process.Start(info);
                                return true;
                            },
                        });
            }

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Glyph = "\xE838",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.E,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        Main.StartProcess(Process.Start, new ProcessStartInfo("explorer", Package.Location));

                        return true;
                    },
                });

            contextMenus.Add(new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = api.GetTranslation("wox_plugin_program_open_in_console"),
                Glyph = "\xE756",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = (context) =>
                {
                    try
                    {
                        Helper.OpenInConsole(Package.Location);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"|Microsoft.Plugin.Program.UWP.ContextMenu| Failed to open {Name} in console, {e.Message}", e);
                        return false;
                    }
                },
            });

            return contextMenus;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentially keeping the process alive, and showing the user an error message")]
        private async void Launch(IPublicAPI api)
        {
            var appManager = new ApplicationActivationHelper.ApplicationActivationManager();
            const string noArgs = "";
            const ApplicationActivationHelper.ActivateOptions noFlags = ApplicationActivationHelper.ActivateOptions.None;
            await Task.Run(() =>
            {
                try
                {
                    appManager.ActivateApplication(UserModelId, noArgs, noFlags, out uint unusedPid);
                }
                catch (Exception)
                {
                    var name = "Plugin: Program";
                    var message = $"Can't start UWP: {DisplayName}";
                    api.ShowMsg(name, message, string.Empty);
                }
            }).ConfigureAwait(false);
        }

        public UWPApplication(IAppxManifestApplication manifestApp, UWP package)
        {
            if (manifestApp == null)
            {
                throw new ArgumentNullException(nameof(manifestApp));
            }

            var hr = manifestApp.GetAppUserModelId(out var tmpUserModelId);
            UserModelId = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpUserModelId);

            hr = manifestApp.GetAppUserModelId(out var tmpUniqueIdentifier);
            UniqueIdentifier = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpUniqueIdentifier);

            hr = manifestApp.GetStringValue("DisplayName", out var tmpDisplayName);
            DisplayName = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpDisplayName);

            hr = manifestApp.GetStringValue("Description", out var tmpDescription);
            Description = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpDescription);

            hr = manifestApp.GetStringValue("BackgroundColor", out var tmpBackgroundColor);
            BackgroundColor = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpBackgroundColor);

            hr = manifestApp.GetStringValue("EntryPoint", out var tmpEntryPoint);
            EntryPoint = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, tmpEntryPoint);

            Package = package ?? throw new ArgumentNullException(nameof(package));

            DisplayName = ResourceFromPri(package.FullName, DisplayName);
            Description = ResourceFromPri(package.FullName, Description);
            logoUri = LogoUriFromManifest(manifestApp);

            Enabled = true;
            CanRunElevated = IfApplicationcanRunElevated();
        }

        private bool IfApplicationcanRunElevated()
        {
            if (EntryPoint == "Windows.FullTrustApplication")
            {
                return true;
            }
            else
            {
                var manifest = Package.Location + "\\AppxManifest.xml";
                if (File.Exists(manifest))
                {
                    var file = File.ReadAllText(manifest);
                    if (file.Contains("TrustLevel=\"mediumIL\"", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal string ResourceFromPri(string packageFullName, string resourceReference)
        {
            const string prefix = "ms-resource:";
            if (!string.IsNullOrWhiteSpace(resourceReference) && resourceReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // magic comes from @talynone
                // https://github.com/talynone/Wox.Plugin.WindowsUniversalAppLauncher/blob/master/StoreAppLauncher/Helpers/NativeApiHelper.cs#L139-L153
                string key = resourceReference.Substring(prefix.Length);
                string parsed;
                if (key.StartsWith("//", StringComparison.Ordinal))
                {
                    parsed = prefix + key;
                }
                else if (key.StartsWith("/", StringComparison.Ordinal))
                {
                    parsed = prefix + "//" + key;
                }
                else if (key.Contains("resources", StringComparison.OrdinalIgnoreCase))
                {
                    parsed = prefix + key;
                }
                else
                {
                    parsed = prefix + "///resources/" + key;
                }

                var outBuffer = new StringBuilder(128);
                string source = $"@{{{packageFullName}? {parsed}}}";
                var capacity = (uint)outBuffer.Capacity;
                var hResult = NativeMethods.SHLoadIndirectString(source, outBuffer, capacity, IntPtr.Zero);
                if (hResult == Hresult.Ok)
                {
                    var loaded = outBuffer.ToString();
                    if (!string.IsNullOrEmpty(loaded))
                    {
                        return loaded;
                    }
                    else
                    {
                        ProgramLogger.LogException(
                            $"|UWP|ResourceFromPri|{Package.Location}|Can't load null or empty result "
                                                    + $"pri {source} in uwp location {Package.Location}", new NullReferenceException());
                        return string.Empty;
                    }
                }
                else
                {
                    // https://github.com/Wox-launcher/Wox/issues/964
                    // known hresult 2147942522:
                    // 'Microsoft Corporation' violates pattern constraint of '\bms-resource:.{1,256}'.
                    // for
                    // Microsoft.MicrosoftOfficeHub_17.7608.23501.0_x64__8wekyb3d8bbwe: ms-resource://Microsoft.MicrosoftOfficeHub/officehubintl/AppManifest_GetOffice_Description
                    // Microsoft.BingFoodAndDrink_3.0.4.336_x64__8wekyb3d8bbwe: ms-resource:AppDescription
                    var e = Marshal.GetExceptionForHR((int)hResult);
                    ProgramLogger.LogException($"|UWP|ResourceFromPri|{Package.Location}|Load pri failed {source} with HResult {hResult} and location {Package.Location}", e);
                    return string.Empty;
                }
            }
            else
            {
                return resourceReference;
            }
        }

        internal string LogoUriFromManifest(IAppxManifestApplication app)
        {
            var logoKeyFromVersion = new Dictionary<PackageVersion, string>
                {
                    { PackageVersion.Windows10, "Square44x44Logo" },
                    { PackageVersion.Windows81, "Square30x30Logo" },
                    { PackageVersion.Windows8, "SmallLogo" },
                };
            if (logoKeyFromVersion.ContainsKey(Package.Version))
            {
                var key = logoKeyFromVersion[Package.Version];
                var hr = app.GetStringValue(key, out var logoUri);
                _ = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, logoUri);
                return logoUri;
            }
            else
            {
                return string.Empty;
            }
        }

        public void UpdatePath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                LogoPath = LogoPathFromUri(logoUri, "contrast-white");
            }
            else
            {
                LogoPath = LogoPathFromUri(logoUri, "contrast-black");
            }
        }

        internal string LogoPathFromUri(string uri, string theme)
        {
            // all https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
            // windows 10 https://msdn.microsoft.com/en-us/library/windows/apps/dn934817.aspx
            // windows 8.1 https://msdn.microsoft.com/en-us/library/windows/apps/hh965372.aspx#target_size
            // windows 8 https://msdn.microsoft.com/en-us/library/windows/apps/br211475.aspx
            string path;
            if (uri.Contains("\\", StringComparison.Ordinal))
            {
                path = Path.Combine(Package.Location, uri);
            }
            else
            {
                // for C:\Windows\MiracastView etc
                path = Path.Combine(Package.Location, "Assets", uri);
            }

            var extension = Path.GetExtension(path);
            if (extension != null)
            {
                var end = path.Length - extension.Length;
                var prefix = path.Substring(0, end);
                var paths = new List<string> { path };

                var scaleFactors = new Dictionary<PackageVersion, List<int>>
                    {
                        // scale factors on win10: https://docs.microsoft.com/en-us/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
                        { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
                        { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
                        { PackageVersion.Windows8, new List<int> { 100 } },
                    };

                if (scaleFactors.ContainsKey(Package.Version))
                {
                    foreach (var factor in scaleFactors[Package.Version])
                    {
                        paths.Add($"{prefix}.scale-{factor}{extension}");
                        paths.Add($"{prefix}.scale-{factor}_{theme}{extension}");
                        paths.Add($"{prefix}.{theme}_scale-{factor}{extension}");
                    }
                }

                paths = paths.OrderByDescending(x => x.Contains(theme, StringComparison.OrdinalIgnoreCase)).ToList();
                var selected = paths.FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(selected))
                {
                    return selected;
                }
                else
                {
                    int appIconSize = 36;
                    var targetSizes = new List<int> { 16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256 }.AsParallel();
                    Dictionary<string, int> pathFactorPairs = new Dictionary<string, int>();

                    foreach (var factor in targetSizes)
                    {
                        string simplePath = $"{prefix}.targetsize-{factor}{extension}";
                        string suffixThemePath = $"{prefix}.targetsize-{factor}_{theme}{extension}";
                        string prefixThemePath = $"{prefix}.{theme}_targetsize-{factor}{extension}";

                        paths.Add(simplePath);
                        paths.Add(suffixThemePath);
                        paths.Add(prefixThemePath);

                        pathFactorPairs.Add(simplePath, factor);
                        pathFactorPairs.Add(suffixThemePath, factor);
                        pathFactorPairs.Add(prefixThemePath, factor);
                    }

                    paths = paths.OrderByDescending(x => x.Contains(theme, StringComparison.OrdinalIgnoreCase)).ToList();
                    var selectedIconPath = paths.OrderBy(x => Math.Abs(pathFactorPairs.GetValueOrDefault(x) - appIconSize)).FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(selectedIconPath))
                    {
                        return selectedIconPath;
                    }
                    else
                    {
                        ProgramLogger.LogException(
                            $"|UWP|LogoPathFromUri|{Package.Location}" +
                            $"|{UserModelId} can't find logo uri for {uri} in package location: {Package.Location}", new FileNotFoundException());
                        return string.Empty;
                    }
                }
            }
            else
            {
                ProgramLogger.LogException(
                    $"|UWP|LogoPathFromUri|{Package.Location}" +
                                                $"|Unable to find extension from {uri} for {UserModelId} " +
                                                $"in package location {Package.Location}", new FileNotFoundException());
                return string.Empty;
            }
        }

        public ImageSource Logo()
        {
            var logo = ImageFromPath(LogoPath);
            return logo;
        }

        private BitmapImage ImageFromPath(string path)
        {
            if (File.Exists(path))
            {
                MemoryStream memoryStream = new MemoryStream();

                byte[] fileBytes = File.ReadAllBytes(path);
                memoryStream.Write(fileBytes, 0, fileBytes.Length);
                memoryStream.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = memoryStream;
                image.EndInit();
                return image;
            }
            else
            {
                ProgramLogger.LogException(
                    $"|UWP|ImageFromPath|{path}" +
                                                $"|Unable to get logo for {UserModelId} from {path} and" +
                                                $" located in {Package.Location}", new FileNotFoundException());
                return new BitmapImage(new Uri(ImageLoader.ErrorIconPath));
            }
        }

        public override string ToString()
        {
            return $"{DisplayName}: {Description}";
        }
    }
}
