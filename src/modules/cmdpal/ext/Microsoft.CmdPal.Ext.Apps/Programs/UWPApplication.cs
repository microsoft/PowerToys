// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Xml;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Helpers;
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
    private const int ListIconSize = 20;
    private const int JumboIconSize = 64;

    private static readonly IFileSystem FileSystem = new FileSystem();
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

    public string JumboLogoPath { get; set; } = string.Empty;

    public LogoType JumboLogoType { get; set; }

    public UWP Package { get; set; }

    private string _logoUri;

    private string _jumboLogoUri;

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
        _logoUri = LogoUriFromManifest(manifestApp);
        _jumboLogoUri = LogoUriFromManifest(manifestApp, jumbo: true);

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

    private static readonly Dictionary<PackageVersion, string> _smallLogoKeyFromVersion = new Dictionary<PackageVersion, string>
        {
            { PackageVersion.Windows10, "Square44x44Logo" },
            { PackageVersion.Windows81, "Square30x30Logo" },
            { PackageVersion.Windows8, "SmallLogo" },
        };

    private static readonly Dictionary<PackageVersion, string> _largeLogoKeyFromVersion = new Dictionary<PackageVersion, string>
    {
        { PackageVersion.Windows10, "Square150x150Logo" },
        { PackageVersion.Windows81, "Square150x150Logo" },
        { PackageVersion.Windows8, "Logo" },
    };

    internal unsafe string LogoUriFromManifest(IAppxManifestApplication* app, bool jumbo = false)
    {
        var logoMap = jumbo ? _largeLogoKeyFromVersion : _smallLogoKeyFromVersion;
        if (logoMap.TryGetValue(Package.Version, out var key))
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
        // Update small logo
        var logo = AppxIconLoader.LogoPathFromUri(_logoUri, theme, ListIconSize, Package);
        if (logo.IsFound)
        {
            LogoPath = logo.LogoPath!;
            LogoType = logo.LogoType;
        }
        else
        {
            LogoPath = string.Empty;
            LogoType = LogoType.Error;
        }

        // Jumbo logo ... small logo can actually provide better result
        var jumboLogo = AppxIconLoader.LogoPathFromUri(_logoUri, theme, JumboIconSize, Package);
        if (jumboLogo.IsFound)
        {
            JumboLogoPath = jumboLogo.LogoPath!;
            JumboLogoType = jumboLogo.LogoType;
        }
        else
        {
            JumboLogoPath = string.Empty;
            JumboLogoType = LogoType.Error;
        }

        if (!jumboLogo.MeetsMinimumSize(JumboIconSize) || !jumboLogo.IsFound)
        {
            var jumboLogoAlt = AppxIconLoader.LogoPathFromUri(_jumboLogoUri, theme, JumboIconSize, Package);
            if (jumboLogoAlt.IsFound)
            {
                JumboLogoPath = jumboLogoAlt.LogoPath!;
                JumboLogoType = jumboLogoAlt.LogoType;
            }
        }
    }

    public AppItem ToAppItem()
    {
        var app = this;
        var iconPath = app.LogoType != LogoType.Error ? app.LogoPath : string.Empty;
        var jumboIconPath = app.JumboLogoType != LogoType.Error ? app.JumboLogoPath : string.Empty;
        var item = new AppItem
        {
            Name = app.Name,
            Subtitle = app.Description,
            Type = UWPApplication.Type(),
            IcoPath = iconPath,
            JumboIconPath = jumboIconPath,
            DirPath = app.Location,
            UserModelId = app.UserModelId,
            IsPackaged = true,
            Commands = app.GetCommands(),
            AppIdentifier = app.GetAppIdentifier(),
            PackageFamilyName = app.Package.FamilyName,
        };
        return item;
    }

    public override string ToString()
    {
        return $"{DisplayName}: {Description}";
    }
}
