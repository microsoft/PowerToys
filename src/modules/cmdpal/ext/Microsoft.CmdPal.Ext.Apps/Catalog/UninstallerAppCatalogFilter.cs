// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Excludes common Win32 uninstaller entries when the corresponding Apps setting is enabled.
/// </summary>
internal sealed partial class UninstallerAppCatalogFilter : IAppCatalogFilter
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    private const string ExecutableSuffix = ".exe";
    private const string ShortcutSuffix = ".lnk";

    private static readonly HashSet<string> _commonUninstallerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninst.exe",
        "unins000.exe",
        "uninst000.exe",
        "uninstall.exe",
    };

    private static readonly string[] _commonUninstallerPrefixes =
    [
        "uninstall",
        "卸载",
        "卸載",
        "видалити",
        "удалить",
        "désinstaller",
        "アンインストール",
        "deïnstalleren",
        "odinstaluj",
        "afinstallere",
        "deinstallieren",
        "삭제",
        "деинсталирај",
        "desinstalar",
        "disinstallare",
        "avinstallere",
        "odinštalovať",
        "kaldır",
        "odinstalovat",
        "إلغاء التثبيت",
        "gỡ bỏ",
        "הסרה",
    ];

    private readonly AllAppsSettings _settings;
    private InterlockedBoolean _hideUninstallers;
    private InterlockedBoolean _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UninstallerAppCatalogFilter"/> class. Initializes a filter backed by the supplied Apps settings.
    /// </summary>
    public UninstallerAppCatalogFilter(AllAppsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _hideUninstallers.Value = settings.HideUninstallers;
        _settings.Settings.SettingsChanged += OnSettingsChanged;
    }

    /// <inheritdoc />
    public bool Includes(AppCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return !_hideUninstallers.Value || !IsUninstaller(item);
    }

    private static bool IsUninstaller(AppCatalogItem item)
    {
        if (item.Payload is not Win32AppPayload program)
        {
            return false;
        }

        if (IsUninstallerPath(program.TargetPath) || IsUninstallerPath(program.LnkFilePath))
        {
            return true;
        }

        foreach (var sourceReference in item.Provenance.References)
        {
            if (IsUninstallerPath(sourceReference.ItemId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUninstallerPath(string? path)
    {
        var fileName = Path.GetFileName(path);
        return !string.IsNullOrEmpty(fileName)
            && (_commonUninstallerNames.Contains(fileName)
            || HasUninstallerPrefix(fileName, ExecutableSuffix)
            || HasUninstallerPrefix(fileName, ShortcutSuffix));
    }

    private static bool HasUninstallerPrefix(string? fileName, string requiredSuffix)
    {
        if (string.IsNullOrEmpty(fileName)
            || !fileName.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in _commonUninstallerPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OnSettingsChanged(object sender, Settings args)
    {
        if (_disposed.Value)
        {
            return;
        }

        var changed = _settings.HideUninstallers
            ? _hideUninstallers.Set()
            : _hideUninstallers.Clear();
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        _settings.Settings.SettingsChanged -= OnSettingsChanged;
        Changed = null;
    }
}
