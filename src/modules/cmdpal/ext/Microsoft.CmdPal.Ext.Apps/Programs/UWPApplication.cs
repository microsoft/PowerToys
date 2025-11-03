// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Xml;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Storage.Packaging.Appx;
using PackageVersion = Microsoft.CmdPal.Ext.Apps.Programs.UWP.PackageVersion;
using Theme = Microsoft.CmdPal.Ext.Apps.Utils.Theme;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[Serializable]
public class UWPApplication : IUWPApplication
{
    private static readonly IFileSystem FileSystem = new FileSystem();
    private static readonly IPath Path = FileSystem.Path;
    private static readonly IFile File = FileSystem.File;

    public string AppListEntry { get; set; } = string.Empty;

    public string UniqueIdentifier { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public string UserModelId { get; set; }

    public string BackgroundColor { get; set; }

    public string EntryPoint { get; set; }

    public string Name => DisplayName;

    public string Location => Package.Location;

    // Localized path based on windows display language
    public string LocationLocalized => Package.LocationLocalized;

    public bool Enabled { get; set; }

    public bool CanRunElevated { get; set; }

    public string LogoPath { get; set; } = string.Empty;

    public LogoType LogoType { get; set; }

    public UWP Package { get; set; }

    private string logoUri;

    private const string ContrastWhite = "contrast-white";

    private const string ContrastBlack = "contrast-black";

    // Function to set the subtitle based on the Type of application
    public static string Type()
    {
        return Resources.packaged_application;
    }

    public string GetAppIdentifier()
    {
        // Use UserModelId for UWP apps as it's unique
        return UserModelId;
    }

    public List<IContextItem> GetCommands()
    {
        List<IContextItem> commands = [];

        if (CanRunElevated)
        {
            commands.Add(
                new CommandContextItem(
                    new RunAsAdminCommand(UniqueIdentifier, string.Empty, true))
                {
                    RequestedShortcut = KeyChords.RunAsAdministrator,
                });

            // We don't add context menu to 'run as different user', because UWP applications normally installed per user and not for all users.
        }

        commands.Add(
            new CommandContextItem(
                new CopyPathCommand(Location))
            {
                RequestedShortcut = KeyChords.CopyFilePath,
            });

        commands.Add(
            new CommandContextItem(
                new OpenFileCommand(Location)
                {
                    Icon = new("\uE838"),
                    Name = Resources.open_location,
                })
            {
                RequestedShortcut = KeyChords.OpenFileLocation,
            });

        commands.Add(
        new CommandContextItem(
            new OpenInConsoleCommand(Package.Location))
        {
            RequestedShortcut = KeyChords.OpenInConsole,
        });

        commands.Add(
            new CommandContextItem(
                new UninstallApplicationConfirmation(this))
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Delete),
                IsCritical = true,
            });

        return commands;
    }

    internal unsafe UWPApplication(IAppxManifestApplication* manifestApp, UWP package)
    {
        ArgumentNullException.ThrowIfNull(manifestApp);

        var hr = manifestApp->GetAppUserModelId(out var tmpUserModelIdPtr);
        UserModelId = ComFreeHelper.GetStringAndFree(hr, tmpUserModelIdPtr);

        manifestApp->GetAppUserModelId(out var tmpUniqueIdentifierPtr);
        UniqueIdentifier = ComFreeHelper.GetStringAndFree(hr, tmpUniqueIdentifierPtr);

        manifestApp->GetStringValue("DisplayName", out var tmpDisplayNamePtr);
        DisplayName = ComFreeHelper.GetStringAndFree(hr, tmpDisplayNamePtr);

        manifestApp->GetStringValue("Description", out var tmpDescriptionPtr);
        Description = ComFreeHelper.GetStringAndFree(hr, tmpDescriptionPtr);

        manifestApp->GetStringValue("BackgroundColor", out var tmpBackgroundColorPtr);
        BackgroundColor = ComFreeHelper.GetStringAndFree(hr, tmpBackgroundColorPtr);

        manifestApp->GetStringValue("EntryPoint", out var tmpEntryPointPtr);
        EntryPoint = ComFreeHelper.GetStringAndFree(hr, tmpEntryPointPtr);

        Package = package ?? throw new ArgumentNullException(nameof(package));

        DisplayName = ResourceFromPri(package.FullName, DisplayName);
        Description = ResourceFromPri(package.FullName, Description);
        logoUri = LogoUriFromManifest(manifestApp);

        Enabled = true;
        CanRunElevated = IfApplicationCanRunElevated();
    }

    private bool IfApplicationCanRunElevated()
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
                try
                {
                    // Check the manifest to verify if the Trust Level for the application is "mediumIL"
                    var file = File.ReadAllText(manifest);
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(file);
                    var xmlRoot = xmlDoc.DocumentElement;
                    var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
                    namespaceManager.AddNamespace("uap10", "http://schemas.microsoft.com/appx/manifest/uap/windows10/10");
                    var trustLevelNode = xmlRoot?.SelectSingleNode("//*[local-name()='Application' and @uap10:TrustLevel]", namespaceManager); // According to https://learn.microsoft.com/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps#create-a-package-manifest-for-the-sparse-package and https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-application#attributes

                    if (trustLevelNode?.Attributes?["uap10:TrustLevel"]?.Value == "mediumIL")
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.Message);
                }
            }
        }

        return false;
    }

    private static string TryLoadIndirectString(string source, Span<char> buffer, string errorContext)
    {
        try
        {
            PInvoke.SHLoadIndirectString(source, buffer).ThrowOnFailure();

            var len = buffer.IndexOf('\0');
            var loaded = len >= 0
                ? buffer[..len].ToString()
                : buffer.ToString();
            return string.IsNullOrEmpty(loaded) ? string.Empty : loaded;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to load resource {source} : {errorContext} : {ex.Message}");
            return string.Empty;
        }
    }

    internal unsafe string ResourceFromPri(string packageFullName, string resourceReference)
    {
        const string prefix = "ms-resource:";

        // Using OrdinalIgnoreCase since this is used internally
        if (!string.IsNullOrWhiteSpace(resourceReference) && resourceReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            // magic comes from @talynone
            // https://github.com/talynone/Wox.Plugin.WindowsUniversalAppLauncher/blob/master/StoreAppLauncher/Helpers/NativeApiHelper.cs#L139-L153
            var key = resourceReference.Substring(prefix.Length);
            string parsed;
            var parsedFallback = string.Empty;

            // Using Ordinal/OrdinalIgnoreCase since these are used internally
            if (key.StartsWith("//", StringComparison.Ordinal))
            {
                parsed = prefix + key;
            }
            else if (key.StartsWith('/'))
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

                // e.g. for Windows Terminal version >= 1.12 DisplayName and Description resources are not in the 'resources' subtree
                parsedFallback = prefix + "///" + key;
            }

            Span<char> outBuffer = stackalloc char[1024];
            var source = $"@{{{packageFullName}? {parsed}}}";

            var loaded = TryLoadIndirectString(source, outBuffer, resourceReference);

            if (!string.IsNullOrEmpty(loaded))
            {
                return loaded;
            }

            if (string.IsNullOrEmpty(parsedFallback))
            {
                // https://github.com/Wox-launcher/Wox/issues/964
                // known hresult 2147942522:
                // 'Microsoft Corporation' violates pattern constraint of '\bms-resource:.{1,256}'.
                // for
                // Microsoft.MicrosoftOfficeHub_17.7608.23501.0_x64__8wekyb3d8bbwe: ms-resource://Microsoft.MicrosoftOfficeHub/officehubintl/AppManifest_GetOffice_Description
                // Microsoft.BingFoodAndDrink_3.0.4.336_x64__8wekyb3d8bbwe: ms-resource:AppDescription
                return string.Empty;
            }

            var sourceFallback = $"@{{{packageFullName}?{parsedFallback}}}";
            return TryLoadIndirectString(sourceFallback, outBuffer, $"{resourceReference} (fallback)");
        }
        else
        {
            return resourceReference;
        }
    }

    private static readonly Dictionary<PackageVersion, string> _logoKeyFromVersion = new Dictionary<PackageVersion, string>
        {
            { PackageVersion.Windows10, "Square44x44Logo" },
            { PackageVersion.Windows81, "Square30x30Logo" },
            { PackageVersion.Windows8, "SmallLogo" },
        };

    internal unsafe string LogoUriFromManifest(IAppxManifestApplication* app)
    {
        if (_logoKeyFromVersion.TryGetValue(Package.Version, out var key))
        {
            var hr = app->GetStringValue(key, out var logoUriFromAppPtr);
            return ComFreeHelper.GetStringAndFree(hr, logoUriFromAppPtr);
        }
        else
        {
            return string.Empty;
        }
    }

    public void UpdateLogoPath(Theme theme)
    {
        LogoPathFromUri(logoUri, theme);
    }

    // scale factors on win10: https://learn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
    private static readonly Dictionary<PackageVersion, List<int>> _scaleFactors = new Dictionary<PackageVersion, List<int>>
        {
            { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
            { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
            { PackageVersion.Windows8, new List<int> { 100 } },
        };

    private bool SetScaleIcons(string path, string colorscheme, bool highContrast = false)
    {
        var extension = Path.GetExtension(path);
        if (extension is not null)
        {
            var end = path.Length - extension.Length;
            var prefix = path.Substring(0, end);
            var paths = new List<string> { };

            if (!highContrast)
            {
                paths.Add(path);
            }

            if (_scaleFactors.TryGetValue(Package.Version, out var factors))
            {
                foreach (var factor in factors)
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

            // By working from the highest resolution to the lowest, we make
            // sure that we use the highest quality possible icon for the app.
            //
            // FirstOrDefault would result in us using the 1x scaled icon
            // always, which is usually too small for our needs.
            for (var i = paths.Count - 1; i >= 0; i--)
            {
                if (File.Exists(paths[i]))
                {
                    LogoPath = paths[i];
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
        }

        return false;
    }

    private bool SetTargetSizeIcon(string path, string colorscheme, bool highContrast = false)
    {
        var extension = Path.GetExtension(path);
        if (extension is not null)
        {
            var end = path.Length - extension.Length;
            var prefix = path.Substring(0, end);
            var paths = new List<string> { };
            const int appIconSize = 36;
            var targetSizes = new List<int> { 16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256 };
            var pathFactorPairs = new Dictionary<string, int>();

            foreach (var factor in targetSizes)
            {
                if (highContrast)
                {
                    var suffixThemePath = $"{prefix}.targetsize-{factor}_{colorscheme}{extension}";
                    var prefixThemePath = $"{prefix}.{colorscheme}_targetsize-{factor}{extension}";
                    paths.Add(suffixThemePath);
                    paths.Add(prefixThemePath);
                    pathFactorPairs.Add(suffixThemePath, factor);
                    pathFactorPairs.Add(prefixThemePath, factor);
                }
                else
                {
                    var simplePath = $"{prefix}.targetsize-{factor}{extension}";
                    var altformUnPlatedPath = $"{prefix}.targetsize-{factor}_altform-unplated{extension}";
                    paths.Add(simplePath);
                    paths.Add(altformUnPlatedPath);
                    pathFactorPairs.Add(simplePath, factor);
                    pathFactorPairs.Add(altformUnPlatedPath, factor);
                }
            }

            // Sort paths by distance to desired app icon size
            var selectedIconPath = string.Empty;
            var closestDistance = int.MaxValue;

            foreach (var p in paths)
            {
                if (File.Exists(p) && pathFactorPairs.TryGetValue(p, out var factor))
                {
                    var distance = Math.Abs(factor - appIconSize);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        selectedIconPath = p;
                    }
                }
            }

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
        // all https://learn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
        // windows 10 https://msdn.microsoft.com/library/windows/apps/dn934817.aspx
        // windows 8.1 https://msdn.microsoft.com/library/windows/apps/hh965372.aspx#target_size
        // windows 8 https://msdn.microsoft.com/library/windows/apps/br211475.aspx
        string path;
        bool isLogoUriSet;

        // Using Ordinal since this is used internally with uri
        if (uri.Contains('\\', StringComparison.Ordinal))
        {
            path = Path.Combine(Package.Location, uri);
        }
        else
        {
            // for C:\Windows\MiracastView, etc.
            path = Path.Combine(Package.Location, "Assets", uri);
        }

        switch (theme)
        {
            case Theme.HighContrastBlack:
            case Theme.HighContrastOne:
            case Theme.HighContrastTwo:
                isLogoUriSet = SetHighContrastIcon(path, ContrastBlack);
                break;
            case Theme.HighContrastWhite:
                isLogoUriSet = SetHighContrastIcon(path, ContrastWhite);
                break;
            case Theme.Light:
                isLogoUriSet = SetColoredIcon(path, ContrastWhite);
                break;
            default:
                isLogoUriSet = SetColoredIcon(path, ContrastBlack);
                break;
        }

        if (!isLogoUriSet)
        {
            LogoPath = string.Empty;
            LogoType = LogoType.Error;
        }
    }

    public AppItem ToAppItem()
    {
        var app = this;
        var iconPath = app.LogoType != LogoType.Error ? app.LogoPath : string.Empty;
        var item = new AppItem()
        {
            Name = app.Name,
            Subtitle = app.Description,
            Type = UWPApplication.Type(),
            IcoPath = iconPath,
            DirPath = app.Location,
            UserModelId = app.UserModelId,
            IsPackaged = true,
            Commands = app.GetCommands(),
            AppIdentifier = app.GetAppIdentifier(),
        };
        return item;
    }

    /*
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
            if (converted is not null)
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
            var memoryStream = new MemoryStream();
            using (var fileStream = File.OpenRead(path))
            {
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = memoryStream;
                image.EndInit();
                return image;
            }
        }
        else
        {
            // ProgramLogger.Exception($"Unable to get logo for {UserModelId} from {path} and located in {Package.Location}", new FileNotFoundException(), GetType(), path);
            return new BitmapImage(new Uri(ImageLoader.ErrorIconPath));
        }
    }
    */

    public override string ToString()
    {
        return $"{DisplayName}: {Description}";
    }
}
