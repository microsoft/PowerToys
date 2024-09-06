// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text;
using System.Xml;

using PackageVersion = AllApps.Programs.UWP.PackageVersion;

namespace AllApps.Programs;

[Serializable]
public class UWPApplication : IProgram
{
    private static readonly FileSystem FileSystem = new();
    private static readonly IPath Path = FileSystem.Path;
    private static readonly IFile File = FileSystem.File;

    public string AppListEntry { get; set; } = string.Empty;

    public string UniqueIdentifier { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UserModelId { get; set; } = string.Empty;

    public string BackgroundColor { get; set; } = string.Empty;

    public string EntryPoint { get; set; } = string.Empty;

    public string Name => DisplayName;

    public string Location => Package.Location;

    // Localized path based on windows display language
    public string LocationLocalized => Package.LocationLocalized;

    public bool Enabled { get; set; }

    public bool CanRunElevated { get; set; }

    public string LogoPath { get; set; } = string.Empty;

    public LogoType LogoType { get; set; }

    public UWP Package { get; set; }

    private readonly string logoUri;

    private const string ContrastWhite = "contrast-white";

    private const string ContrastBlack = "contrast-black";

    // Function to set the subtitle based on the Type of application
    private static string SetSubtitle()
    {
        return string.Empty; // Properties.Resources.powertoys_run_plugin_program_packaged_application;
    }

    public UWPApplication(IAppxManifestApplication manifestApp, UWP package)
    {
        ArgumentNullException.ThrowIfNull(manifestApp);

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
        UpdateLogoPath();

        // logoUri = "";
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
                try
                {
                    // Check the manifest to verify if the Trust Level for the application is "mediumIL"
                    var file = File.ReadAllText(manifest);
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(file);
                    var xmlRoot = xmlDoc.DocumentElement;
                    if (xmlRoot != null)
                    {
                        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
                        namespaceManager.AddNamespace("uap10", "http://schemas.microsoft.com/appx/manifest/uap/windows10/10");
                        var trustLevelNode = xmlRoot.SelectSingleNode("//*[local-name()='Application' and @uap10:TrustLevel]", namespaceManager); // According to https://learn.microsoft.com/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps#create-a-package-manifest-for-the-sparse-package and https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-application#attributes

                        if (trustLevelNode?.Attributes?["uap10:TrustLevel"]?.Value == "mediumIL")
                        {
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    // ProgramLogger.Exception($"Unable to parse manifest file for {DisplayName}", e, MethodBase.GetCurrentMethod().DeclaringType, manifest);
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

            var outBuffer = new StringBuilder(128);

            var source = $"@{{{packageFullName}? {parsed}}}";
            var hResult = Native.SHLoadIndirectString(source, outBuffer, outBuffer.Capacity, IntPtr.Zero);
            if (hResult != 0)
            {
                if (!string.IsNullOrEmpty(parsedFallback))
                {
                    var sourceFallback = $"@{{{packageFullName}? {parsedFallback}}}";
                    hResult = Native.SHLoadIndirectString(sourceFallback, outBuffer, outBuffer.Capacity, IntPtr.Zero);

                    // HRESULT.S_OK
                    if (hResult == 0)
                    {
                        var loaded = outBuffer.ToString();
                        if (!string.IsNullOrEmpty(loaded))
                        {
                            return loaded;
                        }
                        else
                        {
                            // ProgramLogger.Exception($"Can't load null or empty result pri {sourceFallback} in uwp location {Package.Location}", new ArgumentNullException(null), GetType(), Package.Location);
                            return string.Empty;
                        }
                    }
                }

                // https://github.com/Wox-launcher/Wox/issues/964
                // known hresult 2147942522:
                // 'Microsoft Corporation' violates pattern constraint of '\bms-resource:.{1,256}'.
                // for
                // Microsoft.MicrosoftOfficeHub_17.7608.23501.0_x64__8wekyb3d8bbwe: ms-resource://Microsoft.MicrosoftOfficeHub/officehubintl/AppManifest_GetOffice_Description
                // Microsoft.BingFoodAndDrink_3.0.4.336_x64__8wekyb3d8bbwe: ms-resource:AppDescription

                // var e = Marshal.GetExceptionForHR((int)hResult);
                // ProgramLogger.Exception($"Load pri failed {source} with HResult {hResult} and location {Package.Location}", e, GetType(), Package.Location);
                return string.Empty;
            }
            else
            {
                var loaded = outBuffer.ToString();
                if (!string.IsNullOrEmpty(loaded))
                {
                    return loaded;
                }
                else
                {
                    // ProgramLogger.Exception($"Can't load null or empty result pri {source} in uwp location {Package.Location}", new ArgumentNullException(null), GetType(), Package.Location);
                    return string.Empty;
                }
            }
        }
        else
        {
            return resourceReference;
        }
    }

    private static readonly Dictionary<PackageVersion, string> _logoKeyFromVersion = new()
    {
        { PackageVersion.Windows10, "Square44x44Logo" },
        { PackageVersion.Windows81, "Square30x30Logo" },
        { PackageVersion.Windows8, "SmallLogo" },
    };

    internal string LogoUriFromManifest(IAppxManifestApplication app)
    {
        if (_logoKeyFromVersion.TryGetValue(Package.Version, out var key))
        {
            var hr = app.GetStringValue(key, out var logoUriFromApp);
            _ = AppxPackageHelper.CheckHRAndReturnOrThrow(hr, logoUriFromApp);
            return logoUriFromApp;
        }
        else
        {
            return string.Empty;
        }
    }

    public void UpdateLogoPath(/*Theme theme*/)
    {
        LogoPathFromUri(logoUri/*, theme*/);
    }

    // scale factors on win10: https://learn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
    private static readonly Dictionary<PackageVersion, List<int>> _scaleFactors = new()
    {
        { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
        { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
        { PackageVersion.Windows8, new List<int> { 100 } },
    };

    private bool SetScaleIcons(string path, string colorscheme, bool highContrast = false)
    {
        var extension = Path.GetExtension(path);
        if (extension != null)
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
            const int appIconSize = 36;
            var targetSizes = new List<int> { 16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256 }.AsParallel();
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

    internal void LogoPathFromUri(string uri/*, Theme theme*/)
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
            // for C:\Windows\MiracastView etc
            path = Path.Combine(Package.Location, "Assets", uri);
        }

        isLogoUriSet = SetColoredIcon(path, ContrastBlack) || SetColoredIcon(path, ContrastWhite);

        if (!isLogoUriSet)
        {
            LogoPath = string.Empty;
            LogoType = LogoType.Error;

            // ProgramLogger.Exception($"|{UserModelId} can't find logo uri for {uri} in package location: {Package.Location}", new FileNotFoundException(), GetType(), Package.Location);
        }
    }

    private const int _dpiScale100 = 96;

    public override string ToString()
    {
        return $"{DisplayName}: {Description}";
    }
}
