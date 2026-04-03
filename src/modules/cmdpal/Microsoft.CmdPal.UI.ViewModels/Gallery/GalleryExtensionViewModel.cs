// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed partial class GalleryExtensionViewModel : ObservableObject
{
    private static readonly Uri PlaceholderIconUri = new("ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png");
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;
    private static readonly IReadOnlyList<string> EmptyTags = [];
    private const string SourceTypeWinGet = "winget";
    private const string SourceTypeStore = "msstore";
    private const string SourceTypeUrl = "url";
    private const string SourceTypeGitHub = "github";
    private const string SourceTypeWebsite = "website";
    private const string SourceTypeUnknown = "unknown";

    private readonly GalleryExtensionEntry _entry;
    private readonly IExtensionGalleryService _galleryService;
    private readonly IWinGetPackageManagerService? _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService? _winGetOperationTrackerService;
    private readonly IWinGetPackageStatusService? _winGetPackageStatusService;
    private readonly IReadOnlyDictionary<string, GalleryInstallSource> _installSourcesByType;
    private readonly IReadOnlyDictionary<string, GallerySourceInfo> _sourcesByKind;

    public GalleryExtensionViewModel(
        GalleryExtensionEntry entry,
        IExtensionGalleryService galleryService,
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null)
    {
        _entry = entry;
        _galleryService = galleryService;
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetPackageStatusService = winGetPackageStatusService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _installSourcesByType = BuildInstallSourceLookup(entry.InstallSources);
        (Sources, _sourcesByKind) = BuildSourceInfos(_installSourcesByType, entry.Homepage);

        var resolvedIconUri = ResolveIconUri();
        IconUri = resolvedIconUri ?? PlaceholderIconUri;

        if (resolvedIconUri is not null)
        {
            _ = LoadIconSourceAsync(resolvedIconUri);
        }
    }

    public string Id => _entry.Id;

    public string Title => _entry.Title;

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Id;

    public string Description => _entry.Description;

    public string DisplayDescription => !string.IsNullOrWhiteSpace(Description) ? Description : "No description available.";

    public string? ShortDescription => _entry.ShortDescription;

    public string DisplayShortDescription => !string.IsNullOrWhiteSpace(ShortDescription) ? ShortDescription : string.Empty;

    public string AuthorName => _entry.Author?.Name ?? string.Empty;

    public string DisplayAuthorName => !string.IsNullOrWhiteSpace(AuthorName) ? AuthorName : "Unknown author";

    public IReadOnlyList<string> Tags => _entry.Tags ?? EmptyTags;

    public bool HasTags => Tags.Count > 0;

    public string TagsText => BuildTagsText(Tags);

    public string? AuthorUrl => _entry.Author?.Url;

    public string? Homepage => _entry.Homepage;

    public Uri IconUri { get; }

    public ImageSource IconSource
    {
        get => field ??= CreateImageSource(PlaceholderIconUri);
        private set => SetProperty(ref field, value);
    }

    public IReadOnlyList<GallerySourceInfo> Sources { get; }

    public bool HasWinGetSource => HasSource(SourceTypeWinGet);

    public bool HasStoreSource => HasSource(SourceTypeStore);

    public bool HasUrlSource => _installSourcesByType.ContainsKey(SourceTypeUrl) && !string.IsNullOrWhiteSpace(InstallUrl);

    public bool HasHomepage => !string.IsNullOrWhiteSpace(Homepage);

    public bool HasAuthorUrl => !string.IsNullOrWhiteSpace(AuthorUrl);

    public bool HasGitHubSource => HasSource(SourceTypeGitHub);

    public bool HasWebsiteSource => HasSource(SourceTypeWebsite);

    public bool HasUnknownSource => HasSource(SourceTypeUnknown);

    public bool HasAnySourceDetails => Sources.Count > 0;

    public List<GallerySourceInfo> SourcesWithDetails
    {
        get
        {
            List<GallerySourceInfo> withDetails = [];
            for (var i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].HasDetails)
                {
                    withDetails.Add(Sources[i]);
                }
            }

            return withDetails;
        }
    }

    public bool HasSourceMetadataDetails => SourcesWithDetails.Count > 0;

    public bool HasKnownSourceIndicator => Sources.Any(s => s.IsKnown);

    public bool ShowUnknownSourceIndicator => HasUnknownSource || !HasKnownSourceIndicator;

    public bool HasActionableSourceDetails => HasStoreSource || HasWinGetSource || HasHomepage || HasUrlSource;

    public bool ShowNoSourceDetails => !HasActionableSourceDetails;

    public string UnknownSourceTooltip => HasUnknownSource
        ? "This extension has source metadata with an unsupported source type."
        : "Source metadata is not available yet.";

    public string NoSourceMenuText => "Source metadata not available";

    public string NoSourceDetailsText => "This extension does not currently expose install or link metadata in the gallery feed.";

    public string? WinGetId => GetSource(SourceTypeWinGet)?.Id;

    public string? StoreId => GetSource(SourceTypeStore)?.Id;

    public string? InstallUrl => GetSource(SourceTypeGitHub)?.Uri ?? GetSource(SourceTypeWebsite)?.Uri;

    public string WinGetInstallCommand => !string.IsNullOrWhiteSpace(WinGetId) ? $"winget install --id {WinGetId}" : string.Empty;

    public bool CanCopyWinGetInstallCommand => !string.IsNullOrWhiteSpace(WinGetInstallCommand);

    public string WinGetTooltip => !string.IsNullOrWhiteSpace(WinGetId) ? $"WinGet package: {WinGetId}" : "Available on WinGet";

    public string StoreTooltip => !string.IsNullOrWhiteSpace(StoreId) ? $"Microsoft Store product: {StoreId}" : "Available on Microsoft Store";

    public string GitHubTooltip => GetSource(SourceTypeGitHub)?.Uri ?? "GitHub source";

    public string WebsiteTooltip => GetSource(SourceTypeWebsite)?.Uri ?? Homepage ?? "Website source";

    public string WinGetMenuText => !string.IsNullOrWhiteSpace(WinGetId) ? $"WinGet: {WinGetId}" : "WinGet";

    public string StoreMenuText => !string.IsNullOrWhiteSpace(StoreId) ? $"Microsoft Store: {StoreId}" : "Microsoft Store";

    public string GitHubMenuText => "GitHub source";

    public string WebsiteMenuText => "Website source";

    public string? PackageFamilyName => _entry.Detection?.PackageFamilyName;

    public bool IsWinGetAvailable => _winGetPackageManagerService?.State.IsAvailable ?? false;

    public string? WinGetUnavailableMessage => HasWinGetSource && !IsWinGetAvailable ? _winGetPackageManagerService?.State.Message : null;

    public bool ShowWinGetUnavailableMessage => !string.IsNullOrWhiteSpace(WinGetUnavailableMessage);

    public bool ShowInstallViaWinGetButton => HasWinGetSource && (!IsInstalled || IsUpdateAvailable);

    public bool CanInstallViaWinGet => ShowInstallViaWinGetButton && IsWinGetAvailable && !IsWinGetActionInProgress;

    public string InstallViaWinGetText => IsUpdateAvailable ? "Update with WinGet" : "Install with WinGet";

    public bool ShowCancelWinGetActionButton => IsWinGetActionInProgress && CanCancelWinGetAction;

    public bool ShowWinGetActionControls => ShowInstallViaWinGetButton || IsWinGetActionInProgress;

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsInstalledStateKnown { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateStateKnown { get; set; }

    [ObservableProperty]
    public partial bool IsWinGetActionInProgress { get; set; }

    [ObservableProperty]
    public partial bool CanCancelWinGetAction { get; set; }

    [ObservableProperty]
    public partial string? WinGetActionMessage { get; set; }

    [ObservableProperty]
    public partial bool IsWinGetActionIndeterminate { get; set; }

    [ObservableProperty]
    public partial double WinGetActionProgressValue { get; set; }

    public bool ShowInstalledBadge => IsInstalled && !IsUpdateAvailable;

    public bool ShowUpdateBadge => IsUpdateAvailable;

    public bool ShowWinGetActionIndicator => IsWinGetActionInProgress;

    public bool ShowWinGetActionStatus => IsWinGetActionInProgress && !string.IsNullOrWhiteSpace(WinGetActionMessage);

    public string InstallStatusText =>
        IsUpdateAvailable
            ? "Update available"
            : IsInstalled
                ? "Installed"
                : IsInstalledStateKnown
                    ? "Not installed"
                    : "Install status unavailable";

    public string WinGetStatusText =>
        !HasWinGetSource
            ? string.Empty
            : IsUpdateAvailable
                ? "Installed, update available."
                : IsInstalled
                    ? "Installed."
                    : IsInstalledStateKnown
                        ? "Not installed."
                        : "WinGet status unavailable.";

    public bool ShowWinGetStatusDetails => HasWinGetSource && !AreStatusTextsEquivalent(InstallStatusText, WinGetStatusText);

    public bool HasWinGetActionMessage => !string.IsNullOrWhiteSpace(WinGetActionMessage);

    public void ApplyWinGetPackageInfo(WinGetPackageInfo packageInfo)
    {
        IsInstalled = IsInstalled || packageInfo.Status.IsInstalled;
        IsInstalledStateKnown = IsInstalledStateKnown || packageInfo.Status.IsInstalledStateKnown;
        IsUpdateAvailable = packageInfo.Status.IsUpdateAvailable;
        IsUpdateStateKnown = packageInfo.Status.IsUpdateStateKnown;

        if (packageInfo.Details is null)
        {
            return;
        }

        ApplySourceDetails(SourceTypeWinGet, CreateSourceDetails(packageInfo.Details));
    }

    [RelayCommand]
    private void OpenHomepage()
    {
        if (!string.IsNullOrEmpty(Homepage))
        {
            ShellHelpers.OpenInShell(Homepage);
        }
    }

    [RelayCommand]
    private void OpenAuthorPage()
    {
        if (!string.IsNullOrEmpty(AuthorUrl))
        {
            ShellHelpers.OpenInShell(AuthorUrl);
        }
    }

    [RelayCommand]
    private void InstallViaStore()
    {
        if (!string.IsNullOrEmpty(StoreId))
        {
            ShellHelpers.OpenInShell($"ms-windows-store://pdp/?ProductId={StoreId}");
        }
    }

    [RelayCommand]
    private void OpenInstallUrl()
    {
        if (!string.IsNullOrEmpty(InstallUrl))
        {
            ShellHelpers.OpenInShell(InstallUrl);
        }
    }

    [RelayCommand]
    private void CopyWinGetInstall()
    {
        if (string.IsNullOrWhiteSpace(WinGetInstallCommand))
        {
            return;
        }

        ClipboardHelper.SetText(WinGetInstallCommand);
    }

    [RelayCommand(CanExecute = nameof(CanInstallViaWinGet))]
    private async Task InstallViaWinGetAsync()
    {
        if (_winGetPackageManagerService is null || string.IsNullOrWhiteSpace(WinGetId))
        {
            return;
        }

        IsWinGetActionInProgress = true;
        IsWinGetActionIndeterminate = true;
        WinGetActionProgressValue = 0;
        WinGetActionMessage = IsUpdateAvailable ? "Updating with WinGet..." : "Installing with WinGet...";

        try
        {
            var packagesResult = await _winGetPackageManagerService.GetPackagesByIdAsync([WinGetId], includeStoreCatalog: false);
            if (!packagesResult.IsSuccess)
            {
                WinGetActionMessage = packagesResult.ErrorMessage ?? "WinGet couldn't resolve this package.";
                return;
            }

            if (packagesResult.Value is null || !packagesResult.Value.TryGetValue(WinGetId, out var package))
            {
                WinGetActionMessage = "The WinGet package couldn't be found.";
                return;
            }

            var installResult = await _winGetPackageManagerService.InstallPackageAsync(package, skipDependencies: true);
            if (!installResult.Succeeded)
            {
                WinGetActionMessage = installResult.ErrorMessage ?? "The WinGet install failed.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("WinGet install/update failed", ex);
            throw;
        }
        finally
        {
            IsWinGetActionInProgress = false;
            IsWinGetActionIndeterminate = false;
            WinGetActionProgressValue = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelWinGetAction))]
    private void CancelWinGetAction()
    {
        if (_winGetOperationTrackerService is null || string.IsNullOrWhiteSpace(WinGetId))
        {
            return;
        }

        var operation = _winGetOperationTrackerService.GetLatestOperation(WinGetId);
        if (operation is null || operation.IsCompleted || !operation.CanCancel)
        {
            CanCancelWinGetAction = false;
            return;
        }

        if (!_winGetOperationTrackerService.TryCancelOperation(operation.OperationId))
        {
            CanCancelWinGetAction = false;
        }
    }

    public void ApplyTrackedOperation(WinGetPackageOperation operation)
    {
        if (string.IsNullOrWhiteSpace(WinGetId) || !string.Equals(WinGetId, operation.PackageId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CanCancelWinGetAction = operation.CanCancel && !operation.IsCompleted;

        var treatAsUpdate = IsInstalled || IsUpdateAvailable;
        switch (operation.State)
        {
            case WinGetPackageOperationState.Queued:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = operation.Kind == WinGetPackageOperationKind.Uninstall
                    ? "Queued for WinGet uninstall..."
                    : treatAsUpdate
                        ? "Queued for WinGet update..."
                        : "Queued for WinGet install...";
                break;
            case WinGetPackageOperationState.Downloading:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = !operation.ProgressPercent.HasValue;
                WinGetActionProgressValue = operation.ProgressPercent ?? 0;
                WinGetActionMessage = operation.ProgressPercent is uint progressPercent
                    ? $"Downloading with WinGet... {progressPercent}%"
                    : "Downloading with WinGet...";
                break;
            case WinGetPackageOperationState.Installing:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = treatAsUpdate ? "Updating with WinGet..." : "Installing with WinGet...";
                break;
            case WinGetPackageOperationState.Uninstalling:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = "Uninstalling with WinGet...";
                break;
            case WinGetPackageOperationState.PostProcessing:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = "Finishing WinGet operation...";
                break;
            case WinGetPackageOperationState.Succeeded:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 100;
                WinGetActionMessage = operation.Kind == WinGetPackageOperationKind.Uninstall
                    ? "Extension uninstalled with WinGet."
                    : treatAsUpdate
                        ? "Extension updated with WinGet."
                        : "Extension installed with WinGet.";
                ApplyOptimisticTrackedCompletion(operation.Kind);
                break;
            case WinGetPackageOperationState.Canceled:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = "The WinGet operation was canceled.";
                break;
            case WinGetPackageOperationState.Failed:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = operation.ErrorMessage ?? "The WinGet operation failed.";
                break;
            default:
                break;
        }
    }

    private Uri? ResolveIconUri()
    {
        var iconUrl = ToNullIfWhiteSpace(_entry.IconUrl);
        if (iconUrl is null)
        {
            return null;
        }

        if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var resolvedIconUri))
        {
            return null;
        }

        return IsSupportedIconUri(resolvedIconUri) ? resolvedIconUri : null;
    }

    private async Task LoadIconSourceAsync(Uri resolvedIconUri)
    {
        try
        {
            var cachedIconTask = _galleryService.GetCachedIconUriAsync(resolvedIconUri);

            var cachedIconUri = await cachedIconTask;
            if (cachedIconUri is null)
            {
                return;
            }

            IconSource = CreateImageSource(cachedIconUri);
        }
        catch (OperationCanceledException)
        {
            // Best-effort background icon loading.
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load icon for gallery extension '{_entry.Id}' from '{resolvedIconUri}'.", ex);
        }
    }

    private static bool IsSupportedIconUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase);
    }

    private GallerySourceInfo? GetSource(string sourceKind)
    {
        return _sourcesByKind.TryGetValue(sourceKind, out var source) ? source : null;
    }

    private bool HasSource(string sourceKind)
    {
        return _sourcesByKind.ContainsKey(sourceKind);
    }

    private static IReadOnlyDictionary<string, GalleryInstallSource> BuildInstallSourceLookup(List<GalleryInstallSource>? installSources)
    {
        Dictionary<string, GalleryInstallSource> lookup = new(OrdinalIgnoreCase);
        if (installSources is null)
        {
            return lookup;
        }

        foreach (var installSource in installSources)
        {
            var normalizedType = NormalizeSourceType(installSource.Type);
            if (normalizedType is null || lookup.ContainsKey(normalizedType))
            {
                continue;
            }

            lookup[normalizedType] = installSource;
        }

        return lookup;
    }

    private static (IReadOnlyList<GallerySourceInfo> SourceList, IReadOnlyDictionary<string, GallerySourceInfo> SourceByKind) BuildSourceInfos(
        IReadOnlyDictionary<string, GalleryInstallSource> installSourcesByType,
        string? homepage)
    {
        Dictionary<string, GallerySourceInfo> sourcesByKind = new(OrdinalIgnoreCase);

        foreach (var installSource in installSourcesByType.Values)
        {
            var sourceInfo = CreateSourceInfoFromInstallSource(installSource);
            if (sourceInfo is null)
            {
                continue;
            }

            UpsertSourceInfo(sourcesByKind, sourceInfo);
        }

        if (TryCreateSourceInfoFromUri(homepage, out var homepageSource))
        {
            UpsertSourceInfo(sourcesByKind, homepageSource);
        }

        var orderedSources = sourcesByKind
            .Values
            .OrderBy(source => GetSortOrder(source.Kind))
            .ThenBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return (orderedSources, sourcesByKind);
    }

    private static int GetSortOrder(string sourceKind)
    {
        return sourceKind.ToLowerInvariant() switch
        {
            SourceTypeStore => 0,
            SourceTypeWinGet => 1,
            SourceTypeGitHub => 2,
            SourceTypeWebsite => 3,
            SourceTypeUnknown => 99,
            _ => 98,
        };
    }

    private static void UpsertSourceInfo(IDictionary<string, GallerySourceInfo> sourcesByKind, GallerySourceInfo sourceInfo)
    {
        if (sourcesByKind.TryGetValue(sourceInfo.Kind, out var existing))
        {
            sourcesByKind[sourceInfo.Kind] = MergeSourceInfo(existing, sourceInfo);
            return;
        }

        sourcesByKind[sourceInfo.Kind] = sourceInfo;
    }

    private static GallerySourceInfo MergeSourceInfo(GallerySourceInfo existing, GallerySourceInfo incoming)
    {
        return new GallerySourceInfo
        {
            Kind = existing.Kind,
            DisplayName = existing.DisplayName,
            Id = !string.IsNullOrWhiteSpace(existing.Id) ? existing.Id : incoming.Id,
            Uri = !string.IsNullOrWhiteSpace(existing.Uri) ? existing.Uri : incoming.Uri,
            IsKnown = existing.IsKnown || incoming.IsKnown,
        };
    }

    private static GallerySourceInfo? CreateSourceInfoFromInstallSource(GalleryInstallSource installSource)
    {
        var normalizedType = NormalizeSourceType(installSource.Type);
        if (normalizedType is null)
        {
            return null;
        }

        return normalizedType switch
        {
            SourceTypeWinGet => new GallerySourceInfo
            {
                Kind = SourceTypeWinGet,
                DisplayName = "WinGet",
                Id = installSource.Id,
                IsKnown = true,
            },
            SourceTypeStore => new GallerySourceInfo
            {
                Kind = SourceTypeStore,
                DisplayName = "Microsoft Store",
                Id = installSource.Id,
                IsKnown = true,
            },
            SourceTypeUrl => CreateSourceInfoFromUrl(installSource.Uri),
            _ => new GallerySourceInfo
            {
                Kind = SourceTypeUnknown,
                DisplayName = $"Source: {normalizedType}",
                Id = installSource.Id,
                Uri = installSource.Uri,
                IsKnown = false,
            },
        };
    }

    private static GallerySourceInfo CreateSourceInfoFromUrl(string? url)
    {
        if (IsGitHubUri(url))
        {
            return new GallerySourceInfo
            {
                Kind = SourceTypeGitHub,
                DisplayName = "GitHub",
                Uri = url,
                IsKnown = true,
            };
        }

        return new GallerySourceInfo
        {
            Kind = SourceTypeWebsite,
            DisplayName = "Website",
            Uri = url,
            IsKnown = true,
        };
    }

    private static bool TryCreateSourceInfoFromUri(string? uriValue, out GallerySourceInfo sourceInfo)
    {
        sourceInfo = default!;
        if (string.IsNullOrWhiteSpace(uriValue) || !Uri.TryCreate(uriValue, UriKind.Absolute, out _))
        {
            return false;
        }

        sourceInfo = CreateSourceInfoFromUrl(uriValue);
        return true;
    }

    private static string? NormalizeSourceType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return null;
        }

        return sourceType.Trim().ToLowerInvariant();
    }

    private void ApplySourceDetails(string sourceKind, GallerySourceDetails details)
    {
        if (!_sourcesByKind.TryGetValue(sourceKind, out var source))
        {
            return;
        }

        source.Details = details;
        OnPropertyChanged(nameof(SourcesWithDetails));
        OnPropertyChanged(nameof(HasSourceMetadataDetails));
    }

    private static GallerySourceDetails CreateSourceDetails(WinGetPackageDetails details)
    {
        GallerySourceDetails sourceDetails = new()
        {
            Summary = details.Summary,
            Description = details.Description,
            Version = details.Version,
        };

        AddDetail(sourceDetails.Items, "Package", details.Name, uri: null);
        AddDetail(sourceDetails.Items, "Publisher", details.Publisher, details.PublisherUrl);
        AddDetail(sourceDetails.Items, "Author", details.Author, uri: null);
        AddDetail(sourceDetails.Items, "License", details.License, details.LicenseUrl);
        AddDetail(sourceDetails.Items, "Support", null, details.PublisherSupportUrl);
        AddDetail(sourceDetails.Items, "Package page", null, details.PackageUrl);
        AddDetail(sourceDetails.Items, "Release notes", details.ReleaseNotes, details.ReleaseNotesUrl);

        for (var i = 0; i < details.DocumentationLinks.Count; i++)
        {
            var link = details.DocumentationLinks[i];
            AddDetail(sourceDetails.Items, link.Label, null, link.Url);
        }

        for (var i = 0; i < details.Tags.Count; i++)
        {
            var tag = details.Tags[i];
            if (!string.IsNullOrWhiteSpace(tag))
            {
                sourceDetails.Tags.Add(tag);
            }
        }

        return sourceDetails;
    }

    private static void AddDetail(ICollection<GallerySourceDetailItem> target, string label, string? value, string? uri)
    {
        var normalizedValue = ToNullIfWhiteSpace(value);
        var normalizedUri = TryCreateUri(uri);
        if (normalizedValue is null && normalizedUri is null)
        {
            return;
        }

        target.Add(new GallerySourceDetailItem
        {
            Label = label,
            Value = normalizedValue ?? normalizedUri!.AbsoluteUri,
            LinkUri = normalizedUri,
        });
    }

    private static Uri? TryCreateUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri;
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string BuildTagsText(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (var i = 0; i < tags.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(tags[i]))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(tags[i]);
        }

        return builder.ToString();
    }

    private static bool IsGitHubUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreStatusTextsEquivalent(string first, string second)
    {
        return string.Equals(NormalizeStatusText(first), NormalizeStatusText(second), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStatusText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('.');
    }

    public async Task RefreshWinGetPackageInfoAsync(WinGetPackageOperationKind completedOperationKind = WinGetPackageOperationKind.Install)
    {
        if (_winGetPackageStatusService is not null && !string.IsNullOrWhiteSpace(WinGetId))
        {
            var infos = await _winGetPackageStatusService.TryGetPackageInfosAsync([WinGetId]);
            if (infos is not null && infos.TryGetValue(WinGetId, out var packageInfo))
            {
                ApplyWinGetPackageInfo(packageInfo);
                return;
            }
        }

        IsInstalled = completedOperationKind != WinGetPackageOperationKind.Uninstall;
        IsInstalledStateKnown = true;
        IsUpdateAvailable = false;
        IsUpdateStateKnown = true;
    }

    private void ApplyOptimisticTrackedCompletion(WinGetPackageOperationKind completedOperationKind)
    {
        IsInstalled = completedOperationKind != WinGetPackageOperationKind.Uninstall;
        IsInstalledStateKnown = true;
        IsUpdateAvailable = false;
        IsUpdateStateKnown = true;
    }

    private static ImageSource CreateImageSource(Uri iconUri)
    {
        try
        {
            return new BitmapImage(iconUri);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load icon from '{iconUri}'", ex);
            return new BitmapImage(PlaceholderIconUri);
        }
    }

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstalledBadge));
        OnPropertyChanged(nameof(InstallStatusText));
        OnPropertyChanged(nameof(WinGetStatusText));
        OnPropertyChanged(nameof(ShowWinGetStatusDetails));
        OnPropertyChanged(nameof(ShowInstallViaWinGetButton));
        OnPropertyChanged(nameof(CanInstallViaWinGet));
        OnPropertyChanged(nameof(InstallViaWinGetText));
        OnPropertyChanged(nameof(ShowWinGetActionControls));
        InstallViaWinGetCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInstalledStateKnownChanged(bool value)
    {
        OnPropertyChanged(nameof(InstallStatusText));
        OnPropertyChanged(nameof(WinGetStatusText));
        OnPropertyChanged(nameof(ShowWinGetStatusDetails));
    }

    partial void OnIsUpdateAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstalledBadge));
        OnPropertyChanged(nameof(ShowUpdateBadge));
        OnPropertyChanged(nameof(InstallStatusText));
        OnPropertyChanged(nameof(WinGetStatusText));
        OnPropertyChanged(nameof(ShowWinGetStatusDetails));
        OnPropertyChanged(nameof(ShowInstallViaWinGetButton));
        OnPropertyChanged(nameof(CanInstallViaWinGet));
        OnPropertyChanged(nameof(InstallViaWinGetText));
        OnPropertyChanged(nameof(ShowWinGetActionControls));
        InstallViaWinGetCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsWinGetActionInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInstallViaWinGet));
        OnPropertyChanged(nameof(ShowWinGetActionIndicator));
        OnPropertyChanged(nameof(ShowWinGetActionStatus));
        OnPropertyChanged(nameof(ShowCancelWinGetActionButton));
        OnPropertyChanged(nameof(ShowWinGetActionControls));
        InstallViaWinGetCommand.NotifyCanExecuteChanged();
        CancelWinGetActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanCancelWinGetActionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCancelWinGetActionButton));
        CancelWinGetActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnWinGetActionMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasWinGetActionMessage));
        OnPropertyChanged(nameof(ShowWinGetActionStatus));
    }
}
