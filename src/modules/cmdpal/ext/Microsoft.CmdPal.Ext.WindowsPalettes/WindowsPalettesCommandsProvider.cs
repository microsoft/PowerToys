// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Ext.WindowsPalettes.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Metadata;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

public sealed partial class WindowsPalettesCommandsProvider : CommandProvider
{
    private const ushort PalettesContractMajorVersion = 2;
    private const ushort PalettesContractMinorVersion = 0;
    private const string PalettesContractName = "Windows.UI.Palettes.PalettesContract";
    private const int RegdbEClassNotReg = unchecked((int)0x80040154);
    private const int ClassEClassNotAvailable = unchecked((int)0x80040111);
    private const int ErrorModNotFound = unchecked((int)0x8007007E);

    // Kind matching in PaletteCatalog.Open is bidirectional, so these specs include sources that declare
    // the bare root (e.g. "QueryResult"), a wildcard (e.g. "QueryResult.*"), or the CommandPalette host
    // kind, while excluding sources scoped to other hosts.
    // "QueryResult.StartMenu" is included because Windows Settings registers only for it.
    internal static readonly string[] SourceKindSpecs =
    [
        "ZeroState",
        "ZeroState.CommandPalette",
        "QueryResult",
        "QueryResult.CommandPalette",
        "QueryResult.StartMenu",
    ];

    private readonly object _stateLock = new();
    private PaletteCatalog? _catalog;
    private WindowsPalettesPage? _page;
    private ICommandItem[] _topLevelCommands = [];
    private bool _disposed;

    private WindowsPalettesCommandsProvider()
    {
        Id = "com.microsoft.cmdpal.wup.bridge";
        DisplayName = Resources.ProviderDisplayName;
        Icon = WindowsPalettesPage.PalettesIcon;
        Frozen = false;
    }

    public static WindowsPalettesCommandsProvider? TryCreate() =>
        TryCreate(
            static (contractName, majorVersion, minorVersion) =>
                ApiInformation.IsApiContractPresent(contractName, majorVersion, minorVersion));

    internal static WindowsPalettesCommandsProvider? TryCreate(
        Func<string, ushort, ushort, bool> isApiContractPresent,
        Action? initialize = null)
    {
        if (!IsApiAvailable(isApiContractPresent))
        {
            return null;
        }

        var provider = new WindowsPalettesCommandsProvider();
        provider.StartInitialization(initialize ?? provider.InitializeCatalog);
        return provider;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        lock (_stateLock)
        {
            return _topLevelCommands;
        }
    }

    public override ICommand? GetCommand(string id)
    {
        lock (_stateLock)
        {
            return string.Equals(id, WindowsPalettesPage.PageId, StringComparison.Ordinal)
                ? _page
                : null;
        }
    }

    public override ICommandItem? GetCommandItem(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        try
        {
            WindowsPalettesPage? page;
            lock (_stateLock)
            {
                if (string.Equals(id, WindowsPalettesPage.PageId, StringComparison.Ordinal))
                {
                    return _topLevelCommands.Length == 0 ? null : _topLevelCommands[0];
                }

                page = _page;
            }

            return page?.TryRehydrateCommandItem(id);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Command item rehydration failed for '{id}': 0x{ex.HResult:X8} {ex.Message}");
            return null;
        }
    }

    public override void Dispose()
    {
        WindowsPalettesPage? page = null;
        lock (_stateLock)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_catalog is not null)
                {
                    _catalog.Changed -= Catalog_Changed;
                    _catalog = null;
                }

                page = _page;
                _page = null;
                _topLevelCommands = [];
            }
        }

        page?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool IsApiAvailable(Func<string, ushort, ushort, bool> isApiContractPresent)
    {
        try
        {
            return isApiContractPresent(PalettesContractName, PalettesContractMajorVersion, PalettesContractMinorVersion);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Windows Palettes API availability check failed: 0x{ex.HResult:X8} {ex.Message}");
            return false;
        }
    }

    private void StartInitialization(Action initialize)
    {
        _ = Task.Run(
            () =>
            {
                try
                {
                    initialize();
                }
                catch (Exception ex)
                {
                    WupDiagnostics.Write($"Catalog initialization task failed: 0x{ex.HResult:X8} {ex.Message}");
                }
            });
    }

    private void InitializeCatalog()
    {
        PaletteCatalog? catalog = null;
        WindowsPalettesPage? page = null;
        var initialized = false;

        try
        {
            lock (_stateLock)
            {
                if (_disposed || _catalog is not null)
                {
                    return;
                }
            }

            if (!VersionInfo.IsEligible(PalettesContractMajorVersion, PalettesContractMinorVersion))
            {
                WupDiagnostics.Write(
                    $"Windows Palettes API v{VersionInfo.MAJOR}.{VersionInfo.MINOR} is not compatible with v{PalettesContractMajorVersion}.{PalettesContractMinorVersion}.");
                return;
            }

            WupDiagnostics.Write("Opening the palette catalog.");
            catalog = PaletteCatalog.Open(new PaletteKinds(SourceKindSpecs), string.Empty);
            var definitions = catalog.GetPalettes() ?? [];
            page = new WindowsPalettesPage(definitions);
            catalog.Changed += Catalog_Changed;

            lock (_stateLock)
            {
                if (!_disposed && _catalog is null)
                {
                    _catalog = catalog;
                    _page = page;
                    _topLevelCommands = CreateTopLevelCommands(page, definitions.Length);
                    initialized = true;
                }
            }
        }
        catch (Exception ex) when (IsApiMissing(ex))
        {
            WupDiagnostics.Write($"Windows Palettes API unavailable: 0x{ex.HResult:X8} {ex.Message}");
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Catalog initialization failed: 0x{ex.HResult:X8} {ex.Message}");
        }
        finally
        {
            if (!initialized)
            {
                if (catalog is not null)
                {
                    catalog.Changed -= Catalog_Changed;
                }

                page?.Dispose();
            }
        }

        if (initialized)
        {
            WupDiagnostics.Write("Palette catalog initialization completed.");
            RaiseItemsChanged(1);
        }
    }

    internal static bool IsApiMissing(Exception ex) =>
        ex is TypeLoadException or EntryPointNotFoundException ||
        ex.HResult is RegdbEClassNotReg or ClassEClassNotAvailable or ErrorModNotFound;

    private void Catalog_Changed(PaletteCatalog sender, object args)
    {
        PaletteExtensionDefinition[] definitions;
        WindowsPalettesPage? page;
        lock (_stateLock)
        {
            if (!ReferenceEquals(sender, _catalog) || _page is null)
            {
                return;
            }

            page = _page;
        }

        try
        {
            definitions = sender.GetPalettes() ?? [];
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Catalog refresh failed: 0x{ex.HResult:X8} {ex.Message}");
            return;
        }

        // UpdateSources disposes removed sources, which calls into their processes and into Command Palette,
        // so it must not run under _stateLock.
        page.UpdateSources(definitions);
        lock (_stateLock)
        {
            if (!ReferenceEquals(page, _page))
            {
                return;
            }

            _topLevelCommands = CreateTopLevelCommands(page, definitions.Length);
        }

        WupDiagnostics.Write($"Catalog contains {definitions.Length} palette sources.");
        RaiseItemsChanged(1);
    }

    private static ICommandItem[] CreateTopLevelCommands(WindowsPalettesPage page, int sourceCount) =>
    [
        new CommandItem
        {
            Title = Resources.ProviderDisplayName,
            Subtitle = string.Format(CultureInfo.CurrentCulture, Resources.LoadedPalettesSubtitle, sourceCount),
            Command = page,
        },
    ];
}
