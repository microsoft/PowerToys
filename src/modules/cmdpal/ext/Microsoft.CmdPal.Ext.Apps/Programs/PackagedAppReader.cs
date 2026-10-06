// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.Win32;
using Windows.Win32.Storage.Packaging.Appx;
using Windows.Win32.System.Com;

using Theme = Microsoft.CmdPal.Ext.Apps.Utils.Theme;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>Reads one package manifest and resolves each application's display and icon resources.</summary>
internal static class PackagedAppReader
{
    private const int ListIconSize = 20;
    private const int JumboIconSize = 64;

    private static readonly XNamespace Uap3Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
    private static readonly XNamespace Uap5Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
    private static readonly XNamespace Uap8Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/8";
    private static readonly XNamespace Uap10Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
    private static readonly XNamespace DesktopNamespace = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    /// <summary>Returns visible manifest applications; failed application reads leave the scan incomplete.</summary>
    internal static unsafe IReadOnlyList<PackagedAppMetadata> ReadManifest(
        PackageMetadata package,
        Theme theme,
        out bool isComplete,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(package.InstalledLocation, "AppxManifest.xml");
        XDocument manifest;
        using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            manifest = XDocument.Load(file);
        }

        var applicationElements = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        var ns = manifest.Root?.Name.Namespace ?? XNamespace.None;
        foreach (var application in manifest.Root?.Element(ns + "Applications")?.Elements(ns + "Application") ?? [])
        {
            if ((string?)application.Attribute("Id") is { Length: > 0 } id)
            {
                applicationElements[id] = application;
            }
        }

        const uint fileAttributeNormal = 0x80;
        const uint readSharing = 0x40; // STGM_READ | STGM_SHARE_DENY_NONE
        IStream* stream = null;
        var result = PInvoke.SHCreateStreamOnFileEx(path, readSharing, fileAttributeNormal, false, null, &stream);
        using var streamHandle = new SafeComHandle((IntPtr)stream);
        result.ThrowOnFailure();

        result = PInvoke.CoCreateInstance(typeof(AppxFactory).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out IAppxFactory* factory);
        using var factoryHandle = new SafeComHandle((IntPtr)factory);
        result.ThrowOnFailure();

        IAppxManifestReader* reader = null;
        result = factory->CreateManifestReader(stream, &reader);
        using var readerHandle = new SafeComHandle((IntPtr)reader);
        result.ThrowOnFailure();
        IAppxManifestApplicationsEnumerator* applications = null;
        result = reader->GetApplications(&applications);
        using var applicationsHandle = new SafeComHandle((IntPtr)applications);
        result.ThrowOnFailure();

        List<PackagedAppMetadata> items = [];
        isComplete = true;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            applications->GetHasCurrent(out var hasCurrent).ThrowOnFailure();
            if (!hasCurrent)
            {
                break;
            }

            IAppxManifestApplication* application = null;
            result = applications->GetCurrent(&application);
            using (var applicationHandle = new SafeComHandle((IntPtr)application))
            {
                try
                {
                    result.ThrowOnFailure();
                    var id = ReadStringValue(application, "ID");
                    applicationElements.TryGetValue(id, out var element);
                    var app = ReadApplication(application, element, package, theme);
                    if (app is not null)
                    {
                        items.Add(app);
                    }
                }
                catch (Exception exception)
                {
                    isComplete = false;
                    onError?.Invoke(exception);
                    Logger.LogError($"Failed to read an application in {package.FullName}: {exception.Message}");
                }
            }

            applications->MoveNext(out var hasNext).ThrowOnFailure();
            if (!hasNext)
            {
                break;
            }
        }

        return items;
    }

    private static unsafe PackagedAppMetadata? ReadApplication(
        IAppxManifestApplication* application,
        XElement? element,
        PackageMetadata package,
        Theme theme)
    {
        if (ReadStringValue(application, "AppListEntry") == "none")
        {
            return null;
        }

        var result = application->GetAppUserModelId(out var appUserModelId);
        var id = ComFreeHelper.GetStringAndFree(result, appUserModelId);
        var name = ResourceFromPri(package.FullName, ReadStringValue(application, "DisplayName"));
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
        {
            return null;
        }

        var app = new PackagedAppMetadata
        {
            Name = name,
            Description = ResourceFromPri(package.FullName, ReadStringValue(application, "Description")),
            AppUserModelId = id,
            Executable = ReadStringValue(application, "Executable"),
            ExecutionAliases = GetExecutionAliases(element),
            CanRunElevated = ReadStringValue(application, "EntryPoint") == "Windows.FullTrustApplication"
                || (string?)element?.Attribute(Uap10Namespace + "TrustLevel") == "mediumIL",
            Package = package,
        };

        // Windows 8.1 declares its logo schema in a prefixed namespace, not the root namespace.
        var smallLogoUri = ReadStringValue(application, "Square44x44Logo");
        if (string.IsNullOrEmpty(smallLogoUri))
        {
            smallLogoUri = ReadStringValue(application, "Square30x30Logo");
        }

        if (string.IsNullOrEmpty(smallLogoUri))
        {
            smallLogoUri = ReadStringValue(application, "SmallLogo");
        }

        var largeLogoUri = ReadStringValue(application, "Square150x150Logo");
        if (string.IsNullOrEmpty(largeLogoUri))
        {
            largeLogoUri = ReadStringValue(application, "Logo");
        }

        var icon = AppxIconLoader.LogoPathFromUri(smallLogoUri, theme, ListIconSize, package);
        app.LogoPath = icon.IsFound ? icon.LogoPath! : string.Empty;
        var jumboIcon = AppxIconLoader.LogoPathFromUri(smallLogoUri, theme, JumboIconSize, package);
        if (!jumboIcon.MeetsMinimumSize(JumboIconSize) || !jumboIcon.IsFound)
        {
            var alternative = AppxIconLoader.LogoPathFromUri(largeLogoUri, theme, JumboIconSize, package);
            if (alternative.IsFound)
            {
                jumboIcon = alternative;
            }
        }

        app.JumboLogoPath = jumboIcon.IsFound ? jumboIcon.LogoPath! : string.Empty;
        return app;
    }

    private static unsafe string ReadStringValue(IAppxManifestApplication* application, string name)
    {
        // Supported optional values return S_OK with a null pointer when absent.
        var result = application->GetStringValue(name, out var value);
        return ComFreeHelper.GetStringAndFree(result, value);
    }

    private static IReadOnlyList<string> GetExecutionAliases(XElement? application)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in application?.Element(application.Name.Namespace + "Extensions")?.Elements() ?? [])
        {
            if ((extension.Name != Uap3Namespace + "Extension" && extension.Name != Uap5Namespace + "Extension")
                || (string?)extension.Attribute("Category") != "windows.appExecutionAlias")
            {
                continue;
            }

            foreach (var element in extension.Element(extension.Name.Namespace + "AppExecutionAlias")?.Elements() ?? [])
            {
                if (element.Name != DesktopNamespace + "ExecutionAlias"
                    && element.Name != Uap5Namespace + "ExecutionAlias"
                    && element.Name != Uap8Namespace + "ExecutionAlias")
                {
                    continue;
                }

                var alias = (string?)element.Attribute("Alias");
                if (!string.IsNullOrWhiteSpace(alias)
                    && alias.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && alias.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    aliases.Add(alias);
                }
            }
        }

        return [.. aliases];
    }

    private static string TryLoadIndirectString(string source, Span<char> buffer, string errorContext)
    {
        try
        {
            PInvoke.SHLoadIndirectString(source, buffer).ThrowOnFailure();
            var length = buffer.IndexOf('\0');
            return length >= 0 ? buffer[..length].ToString() : buffer.ToString();
        }
        catch (Exception exception)
        {
            Logger.LogError($"Unable to load resource {source} : {errorContext} : {exception.Message}");
            return string.Empty;
        }
    }

    private static string ResourceFromPri(string packageFullName, string resourceReference)
    {
        const string prefix = "ms-resource:";
        if (string.IsNullOrWhiteSpace(resourceReference) || !resourceReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceReference;
        }

        var key = resourceReference.Substring(prefix.Length);
        string parsed;
        var parsedFallback = string.Empty;
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

            // Terminal also uses resource keys outside the resources subtree.
            parsedFallback = prefix + "///" + key;
        }

        Span<char> buffer = stackalloc char[1024];
        var loaded = TryLoadIndirectString($"@{{{packageFullName}? {parsed}}}", buffer, resourceReference);
        return !string.IsNullOrEmpty(loaded) || string.IsNullOrEmpty(parsedFallback)
            ? loaded
            : TryLoadIndirectString($"@{{{packageFullName}?{parsedFallback}}}", buffer, $"{resourceReference} (fallback)");
    }
}
