// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

internal sealed partial class SettingsAppVisibilityStore : IAppVisibilityStore
{
    private readonly AllAppsSettings _settings;
    private ExclusionPatterns _patterns;

    public SettingsAppVisibilityStore(AllAppsSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _patterns = ReadPatterns();
        _settings.Settings.SettingsChanged += OnSettingsChanged;
        _settings.HiddenAppsChanged += OnHiddenAppsChanged;
    }

    public event EventHandler? Changed;

    public AppVisibility GetVisibility(AppCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var patterns = Volatile.Read(ref _patterns);
        var matches = item.Payload switch
        {
            Win32AppPayload app => Matches(patterns.Names, app.Name)
                || Matches(patterns.Names, app.DisplayName)
                || MatchesPath(patterns.Paths, app.TargetPath)
                || MatchesPath(patterns.Paths, app.LnkFilePath)
                || MatchesPath(patterns.Paths, app.AppExecutionAliasTargetPath),
            PackagedAppSnapshot app => Matches(patterns.Names, app.Name)
                || MatchesPath(patterns.Paths, app.PackageLocation),
            _ => false,
        };

        // Merged representations retain their shortcut and executable paths in provenance and search terms.
        matches = matches || (patterns.Paths.Length > 0
            && (item.Provenance.References.Any(source => Path.IsPathFullyQualified(source.ItemId) && MatchesPath(patterns.Paths, source.ItemId))
                || item.MatchTerms.Any(term => Path.IsPathFullyQualified(term) && MatchesPath(patterns.Paths, term))));

        return matches ? AppVisibility.HiddenByPattern
            : item.IdentityAliases.Any(_settings.IsAppHidden) ? AppVisibility.Hidden : AppVisibility.Visible;
    }

    public bool SetHidden(AppCatalogItem item, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _settings.SetAppHidden(item.IdentityAliases, hidden);
    }

    public void Persist()
    {
        _settings.SaveSettings();
    }

    private static bool Matches(IReadOnlyList<string> patterns, string? value)
        => !string.IsNullOrEmpty(value)
            && patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, value, ignoreCase: true));

    // Backslashes escape characters in MatchesSimpleExpression, so normalize Windows separators first.
    private static bool MatchesPath(IReadOnlyList<string> patterns, string? path)
        => patterns.Count > 0 && Matches(patterns, path?.Replace('\\', '/'));

    private ExclusionPatterns ReadPatterns() => new(
        [.. _settings.ExcludedAppNames.Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0)],
        [.. _settings.ExcludedAppPaths.Select(pattern => pattern.Trim().Replace('\\', '/')).Where(pattern => pattern.Length > 0)]);

    private void OnSettingsChanged(object sender, Settings args)
    {
        var updated = ReadPatterns();
        var previous = Interlocked.Exchange(ref _patterns, updated);
        if (!previous.Names.SequenceEqual(updated.Names, StringComparer.OrdinalIgnoreCase)
            || !previous.Paths.SequenceEqual(updated.Paths, StringComparer.OrdinalIgnoreCase))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnHiddenAppsChanged(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _settings.Settings.SettingsChanged -= OnSettingsChanged;
        _settings.HiddenAppsChanged -= OnHiddenAppsChanged;
        Changed = null;
    }

    private sealed record ExclusionPatterns(string[] Names, string[] Paths);
}
