// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManagedCommon;
using Microsoft.Plugin.Program.Logger;
using Microsoft.Plugin.Program.Win32;
using Wox.Infrastructure;
using Wox.Infrastructure.Image;
using Wox.Plugin;
using Wox.Plugin.Logger;
using Wox.Plugin.SharedCommands;
using static Microsoft.Plugin.Program.Programs.UWP;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public class UWPApplication : IProgram
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

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

        public LogoType LogoType { get; set; }

        public UWP Package { get; set; }

        private string logoUri;

        private const string ContrastWhite = "contrast-white";

        private const string ContrastBlack = "contrast-black";

        // Function to calculate the score of a result
        private int Score(string query)
        {
            var displayNameMatch = StringMatcher.FuzzySearch(query, DisplayName);
            var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
            var score = new[] { displayNameMatch.Score, descriptionMatch.Score / 2 }.Max();
            return score;
        }

        // Function to set the subtitle based on the Type of application
        private static string SetSubtitle()
        {
            return Properties.Resources.powertoys_run_plugin_program_packaged_application;
        }

        public Result Result(string query, string queryArguments, IPublicAPI api)
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
                SubTitle = SetSubtitle(),
                Icon = Logo,
                Score = score,
                ContextData = this,
                ProgramArguments = queryArguments,
                Action = e =>
                {
                    Launch(api, queryArguments);
                    return true;
                },
            };

            // To set the title to always be the displayname of the packaged application
            result.Title = DisplayName;
            result.SetTitleHighlightData(StringMatcher.FuzzySearch(query, Name).MatchData);

            // Using CurrentCulture since this is user facing
            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", Properties.Resources.powertoys_run_plugin_program_file_name, result.Title);
            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", Properties.Resources.powertoys_run_plugin_program_file_path, Package.Location);
            result.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally keeping the process alive.")]
        public List<ContextMenuResult> ContextMenus(string queryArguments, IPublicAPI api)
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
                            Title = Properties.Resources.wox_plugin_program_run_as_administrator,
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
                                info.Arguments = queryArguments;
                                Process.Start(info);
                                return true;
                            },
                        });
            }

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_open_containing_folder,
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
                Title = Properties.Resources.wox_plugin_program_open_in_console,
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
                        Log.Exception($"Failed to open {Name} in console, {e.Message}", e, GetType());
                        return false;
                    }
                },
            });

            return contextMenus;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally keeping the process alive, and showing the user an error message")]
        private async void Launch(IPublicAPI api, string queryArguments)
        {
            var appManager = new ApplicationActivationHelper.ApplicationActivationManager();
            const ApplicationActivationHelper.ActivateOptions noFlags = ApplicationActivationHelper.ActivateOptions.None;
            await Task.Run(() =>
            {
                try
                {
                    appManager.ActivateApplication(UserModelId, queryArguments, noFlags, out var unusedPid);
                }
                catch (Exception)
                {
                    var name = "Plugin: " + Properties.Resources.wox_plugin_program_plugin_name;
                    var message = $"{Properties.Resources.powertoys_run_plugin_program_uwp_failed}: {DisplayName}";
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

                    // Using OrdinalIgnoreCase since this is used internally
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

            // Using OrdinalIgnoreCase since this is used internally
            if (!string.IsNullOrWhiteSpace(resourceReference) && resourceReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // magic comes from @talynone
                // https://github.com/talynone/Wox.Plugin.WindowsUniversalAppLauncher/blob/master/StoreAppLauncher/Helpers/NativeApiHelper.cs#L139-L153
                string key = resourceReference.Substring(prefix.Length);
                string parsed;

                // Using Ordinal/OrdinalIgnoreCase since these are used internally
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
                        ProgramLogger.Exception($"Can't load null or empty result pri {source} in uwp location {Package.Location}", new NullReferenceException(), GetType(), Package.Location);

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
                    ProgramLogger.Exception($"Load pri failed {source} with HResult {hResult} and location {Package.Location}", e, GetType(), Package.Location);

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
            LogoPathFromUri(this.logoUri, theme);
        }

        private bool SetScaleIcons(string path, string colorscheme, bool highContrast = false)
        {
            var extension = Path.GetExtension(path);
            if (extension != null)
            {
                var end = path.Length - extension.Length;
                var prefix = path.Substring(0, end);
                var paths = new List<string> { };
                var scaleFactors = new Dictionary<PackageVersion, List<int>>
                    {
                        // scale factors on win10: https://docs.microsoft.com/en-us/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
                        { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
                        { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
                        { PackageVersion.Windows8, new List<int> { 100 } },
                    };

                if (!highContrast)
                {
                    paths.Add(path);
                }

                if (scaleFactors.ContainsKey(Package.Version))
                {
                    foreach (var factor in scaleFactors[Package.Version])
                    {
                        if (highContrast)
                        {
                            paths.Add($"{prefix}.scale-{factor}_{colorscheme}{extension}");
                            paths.Add($"{prefix}.{colorscheme}_scale-{factor}{extension}");
                        }
                        else
                        {
                            paths.Add($"{prefix}.scale-{factor}{extension}");
                        }
                    }
                }

                var selectedIconPath = paths.FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(selectedIconPath))
                {
                    LogoPath = selectedIconPath;
                    if (highContrast)
                    {
                        LogoType = LogoType.HighContrast;
                    }
                    else
                    {
                        LogoType = LogoType.Colored;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool SetTargetSizeIcon(string path, string colorscheme, bool highContrast = false)
        {
            var extension = Path.GetExtension(path);
            if (extension != null)
            {
                var end = path.Length - extension.Length;
                var prefix = path.Substring(0, end);
                var paths = new List<string> { };
                int appIconSize = 36;
                var targetSizes = new List<int> { 16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256 }.AsParallel();
                var pathFactorPairs = new Dictionary<string, int>();

                foreach (var factor in targetSizes)
                {
                    if (highContrast)
                    {
                        string suffixThemePath = $"{prefix}.targetsize-{factor}_{colorscheme}{extension}";
                        string prefixThemePath = $"{prefix}.{colorscheme}_targetsize-{factor}{extension}";
                        paths.Add(suffixThemePath);
                        paths.Add(prefixThemePath);
                        pathFactorPairs.Add(suffixThemePath, factor);
                        pathFactorPairs.Add(prefixThemePath, factor);
                    }
                    else
                    {
                        string simplePath = $"{prefix}.targetsize-{factor}{extension}";
                        string altformUnPlatedPath = $"{prefix}.targetsize-{factor}_altform-unplated{extension}";
                        paths.Add(simplePath);
                        paths.Add(altformUnPlatedPath);
                        pathFactorPairs.Add(simplePath, factor);
                        pathFactorPairs.Add(altformUnPlatedPath, factor);
                    }
                }

                var selectedIconPath = paths.OrderBy(x => Math.Abs(pathFactorPairs.GetValueOrDefault(x) - appIconSize)).FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(selectedIconPath))
                {
                    LogoPath = selectedIconPath;
                    if (highContrast)
                    {
                        LogoType = LogoType.HighContrast;
                    }
                    else
                    {
                        LogoType = LogoType.Colored;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool SetColoredIcon(string path, string colorscheme)
        {
            var isSetColoredScaleIcon = SetScaleIcons(path, colorscheme);
            if (isSetColoredScaleIcon)
            {
                return true;
            }

            var isSetColoredTargetIcon = SetTargetSizeIcon(path, colorscheme);
            if (isSetColoredTargetIcon)
            {
                return true;
            }

            var isSetHighContrastScaleIcon = SetScaleIcons(path, colorscheme, true);
            if (isSetHighContrastScaleIcon)
            {
                return true;
            }

            var isSetHighContrastTargetIcon = SetTargetSizeIcon(path, colorscheme, true);
            if (isSetHighContrastTargetIcon)
            {
                return true;
            }

            return false;
        }

        private bool SetHighContrastIcon(string path, string colorscheme)
        {
            var isSetHighContrastScaleIcon = SetScaleIcons(path, colorscheme, true);
            if (isSetHighContrastScaleIcon)
            {
                return true;
            }

            var isSetHighContrastTargetIcon = SetTargetSizeIcon(path, colorscheme, true);
            if (isSetHighContrastTargetIcon)
            {
                return true;
            }

            var isSetColoredScaleIcon = SetScaleIcons(path, colorscheme);
            if (isSetColoredScaleIcon)
            {
                return true;
            }

            var isSetColoredTargetIcon = SetTargetSizeIcon(path, colorscheme);
            if (isSetColoredTargetIcon)
            {
                return true;
            }

            return false;
        }

        internal void LogoPathFromUri(string uri, Theme theme)
        {
            // all https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
            // windows 10 https://msdn.microsoft.com/en-us/library/windows/apps/dn934817.aspx
            // windows 8.1 https://msdn.microsoft.com/en-us/library/windows/apps/hh965372.aspx#target_size
            // windows 8 https://msdn.microsoft.com/en-us/library/windows/apps/br211475.aspx
            string path;
            bool isLogoUriSet;

            // Using Ordinal since this is used internally with uri
            if (uri.Contains("\\", StringComparison.Ordinal))
            {
                path = Path.Combine(Package.Location, uri);
            }
            else
            {
                // for C:\Windows\MiracastView etc
                path = Path.Combine(Package.Location, "Assets", uri);
            }

            if (theme == Theme.HighContrastBlack || theme == Theme.HighContrastOne || theme == Theme.HighContrastTwo)
            {
                isLogoUriSet = SetHighContrastIcon(path, ContrastBlack);
            }
            else if (theme == Theme.HighContrastWhite)
            {
                isLogoUriSet = SetHighContrastIcon(path, ContrastWhite);
            }
            else if (theme == Theme.Light)
            {
                isLogoUriSet = SetColoredIcon(path, ContrastWhite);
            }
            else
            {
                isLogoUriSet = SetColoredIcon(path, ContrastBlack);
            }

            if (!isLogoUriSet)
            {
                LogoPath = string.Empty;
                LogoType = LogoType.Error;
                ProgramLogger.Exception($"|{UserModelId} can't find logo uri for {uri} in package location: {Package.Location}", new FileNotFoundException(), GetType(), Package.Location);
            }
        }

        public ImageSource Logo()
        {
            if (LogoType == LogoType.Colored)
            {
                var logo = ImageFromPath(LogoPath);
                var platedImage = PlatedImage(logo);
                return platedImage;
            }
            else
            {
                return ImageFromPath(LogoPath);
            }
        }

        private const int _dpiScale100 = 96;

        private ImageSource PlatedImage(BitmapImage image)
        {
            if (!string.IsNullOrEmpty(BackgroundColor))
            {
                string currentBackgroundColor;
                if (BackgroundColor == "transparent")
                {
                    // Using InvariantCulture since this is internal
                    currentBackgroundColor = SystemParameters.WindowGlassBrush.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    currentBackgroundColor = BackgroundColor;
                }

                var padding = 8;
                var width = image.Width + (2 * padding);
                var height = image.Height + (2 * padding);
                var x = 0;
                var y = 0;

                var group = new DrawingGroup();
                var converted = ColorConverter.ConvertFromString(currentBackgroundColor);
                if (converted != null)
                {
                    var color = (Color)converted;
                    var brush = new SolidColorBrush(color);
                    var pen = new Pen(brush, 1);
                    var backgroundArea = new Rect(0, 0, width, height);
                    var rectangleGeometry = new RectangleGeometry(backgroundArea, 8, 8);
                    var rectDrawing = new GeometryDrawing(brush, pen, rectangleGeometry);
                    group.Children.Add(rectDrawing);

                    var imageArea = new Rect(x + padding, y + padding, image.Width, image.Height);
                    var imageDrawing = new ImageDrawing(image, imageArea);
                    group.Children.Add(imageDrawing);

                    // http://stackoverflow.com/questions/6676072/get-system-drawing-bitmap-of-a-wpf-area-using-visualbrush
                    var visual = new DrawingVisual();
                    var context = visual.RenderOpen();
                    context.DrawDrawing(group);
                    context.Close();

                    var bitmap = new RenderTargetBitmap(
                        Convert.ToInt32(width),
                        Convert.ToInt32(height),
                        _dpiScale100,
                        _dpiScale100,
                        PixelFormats.Pbgra32);

                    bitmap.Render(visual);

                    return bitmap;
                }
                else
                {
                    ProgramLogger.Exception($"Unable to convert background string {BackgroundColor} to color for {Package.Location}", new InvalidOperationException(), GetType(), Package.Location);

                    return new BitmapImage(new Uri(Constant.ErrorIcon));
                }
            }
            else
            {
                // todo use windows theme as background
                return image;
            }
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
                ProgramLogger.Exception($"Unable to get logo for {UserModelId} from {path} and located in {Package.Location}", new FileNotFoundException(), GetType(), path);
                return new BitmapImage(new Uri(ImageLoader.ErrorIconPath));
            }
        }

        public override string ToString()
        {
            return $"{DisplayName}: {Description}";
        }
    }

    public enum LogoType
    {
        Error,
        Colored,
        HighContrast,
    }
}
