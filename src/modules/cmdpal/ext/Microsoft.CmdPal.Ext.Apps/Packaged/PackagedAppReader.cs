// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.Win32;
using Windows.Win32.Storage.Packaging.Appx;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Packaged;

/// <summary>Reads one package manifest, resolving display text and retaining logical logo references.</summary>
internal static class PackagedAppReader
{
    private static readonly XNamespace Uap3Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
    private static readonly XNamespace Uap5Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
    private static readonly XNamespace Uap8Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/8";
    private static readonly XNamespace Uap10Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
    private static readonly XNamespace DesktopNamespace = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    /// <summary>Returns visible manifest applications; failed application reads leave the scan incomplete.</summary>
    internal static unsafe IReadOnlyList<PackagedAppMetadata> ReadManifest(
        PackageMetadata package,
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
                    var app = ReadApplication(application, element, package);
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

    /// <summary>Extracts one recognized Edge PWA launch from the manifest XML already loaded by discovery.</summary>
    /// <param name="application">The application element in the loaded manifest.</param>
    /// <returns>Complete launch metadata, or <see langword="null"/> when equivalence cannot be established.</returns>
    internal static EdgePwaLaunchInfo? ReadEdgePwaLaunchInfo(XElement? application)
    {
        var root = application?.Document?.Root;
        if (application is null || root is null
            || (string?)application.Attribute(Uap10Namespace + "HostId") != "PWA"
            || !string.IsNullOrEmpty((string?)application.Attribute("Executable"))
            || !string.IsNullOrEmpty((string?)application.Attribute("EntryPoint"))
            || !string.IsNullOrEmpty((string?)application.Attribute("StartPage")))
        {
            return null;
        }

        var ns = root.Name.Namespace;
        var hosts = root.Element(ns + "Dependencies")?.Elements(Uap10Namespace + "HostRuntimeDependency").Take(2).ToArray();
        var extensions = application.Element(ns + "Extensions")?.Elements(Uap3Namespace + "Extension")
            .Where(extension => (string?)extension.Attribute("Category") == "windows.appExtension")
            .Elements(Uap3Namespace + "AppExtension")
            .Where(extension => ((string?)extension.Attribute("Name"))?.StartsWith("com.ms.webapp.internals.", StringComparison.Ordinal) == true)
            .Take(2).ToArray();
        if (hosts is not { Length: 1 } || extensions is not { Length: 1 }
            || (string?)extensions[0].Attribute("Name") != "com.ms.webapp.internals.2")
        {
            return null;
        }

        var info = new EdgePwaLaunchInfo
        {
            PackagePublisher = (string?)root.Element(ns + "Identity")?.Attribute("Publisher") ?? string.Empty,
            HostPackageName = (string?)hosts[0].Attribute("Name") ?? string.Empty,
            HostPackagePublisher = (string?)hosts[0].Attribute("Publisher") ?? string.Empty,
            HostId = (string?)application.Attribute(Uap10Namespace + "HostId") ?? string.Empty,
            Parameters = (string?)application.Attribute(Uap10Namespace + "Parameters") ?? string.Empty,
            LaunchContext = (string?)extensions[0].Attribute("Description") ?? string.Empty,
        };
        return info.IsSupported ? info : null;
    }

    private static unsafe PackagedAppMetadata? ReadApplication(
        IAppxManifestApplication* application,
        XElement? element,
        PackageMetadata package)
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
            IsWebApp = (string?)element?.Attribute(Uap10Namespace + "HostId") == "PWA",
            EdgePwaLaunch = ReadEdgePwaLaunchInfo(element),
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

        app.SmallLogoUri = smallLogoUri;
        app.LargeLogoUri = largeLogoUri;
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
