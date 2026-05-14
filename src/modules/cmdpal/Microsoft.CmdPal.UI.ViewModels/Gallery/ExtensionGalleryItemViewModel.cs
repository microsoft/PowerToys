// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed partial class ExtensionGalleryItemViewModel : ObservableObject
{
    private static readonly Uri PlaceholderIconUri = new("ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png");
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;
    private static readonly IReadOnlyList<string> EmptyTags = [];
    private static readonly Action<ILogger, Exception?> LogWinGetInstallFailedMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogWinGetInstallFailed)),
            "WinGet install/update failed.");

    private static readonly Action<ILogger, string, Exception?> LogIconLoadFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, nameof(LogIconLoadFailed)),
            "Failed to load icon from '{IconUri}'.");

    private const string SourceTypeWinGet = "winget";
    private const string SourceTypeStore = "msstore";
    private const string SourceTypeUrl = "url";
    private const string SourceTypeGitHub = "github";
    private const string SourceTypeWebsite = "website";
    private const string SourceTypeUnknown = "unknown";

    private readonly GalleryExtensionEntry _entry;
    private readonly ILogger<ExtensionGalleryItemViewModel> _logger;
    private readonly IWinGetPackageManagerService? _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService? _winGetOperationTrackerService;
    private readonly IWinGetPackageStatusService? _winGetPackageStatusService;
    private readonly IReadOnlyDictionary<string, GalleryInstallSource> _installSourcesByType;
    private readonly IReadOnlyDictionary<string, GallerySourceViewModel> _sourcesByKind;

    public ExtensionGalleryItemViewModel(
        GalleryExtensionEntry entry,
        ILogger<ExtensionGalleryItemViewModel> logger,
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null)
    {
        _entry = entry;
        _logger = logger;
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetPackageStatusService = winGetPackageStatusService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _installSourcesByType = BuildInstallSourceLookup(entry.InstallSources);
        (Sources, _sourcesByKind) = BuildSourceInfos(_installSourcesByType, entry.Homepage);
        Screenshots = BuildScreenshots(entry.ScreenshotUrls);

        var resolvedIconUri = ResolveIconUri();
        IconUri = resolvedIconUri ?? PlaceholderIconUri;
    }

    public string Id => _entry.Id;

    public string Title => _entry.Title;

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Id;

    public string Description => _entry.Description;

    public string DisplayDescription => !string.IsNullOrWhiteSpace(Description) ? Description : Resources.gallery_item_no_description;

    public string? ShortDescription => _entry.ShortDescription;

    public string DisplayShortDescription => !string.IsNullOrWhiteSpace(ShortDescription) ? ShortDescription : string.Empty;

    public string AuthorName => _entry.Author?.Name ?? string.Empty;

    public string DisplayAuthorName => !string.IsNullOrWhiteSpace(AuthorName) ? AuthorName : Resources.gallery_item_unknown_author;

    public IReadOnlyList<string> Tags => _entry.Tags ?? EmptyTags;

    public bool HasTags => Tags.Count > 0;

    public string TagsText => BuildTagsText(Tags);

    public string? AuthorUrl => _entry.Author?.Url;

    public string? Homepage => _entry.Homepage;

    public Uri IconUri { get; }

    public ImageSource IconSource
    {
        get => field ??= CreateImageSource(IconUri);
        private set => SetProperty(ref field, value);
    }

    public IReadOnlyList<ExtensionGalleryScreenshotViewModel> Screenshots { get; }

    public bool HasScreenshots => Screenshots.Count > 0;

    public IReadOnlyList<GallerySourceViewModel> Sources { get; }

    public bool HasWinGetSource => HasSource(SourceTypeWinGet);

    public bool HasStoreSource => HasSource(SourceTypeStore);

    public bool HasUrlSource => _installSourcesByType.ContainsKey(SourceTypeUrl) && !string.IsNullOrWhiteSpace(InstallUrl);

    public bool HasHomepage => !string.IsNullOrWhiteSpace(Homepage);

    public bool HasAuthorUrl => !string.IsNullOrWhiteSpace(AuthorUrl);

    public bool HasGitHubSource => HasSource(SourceTypeGitHub);

    public bool HasWebsiteSource => HasSource(SourceTypeWebsite);

    public bool HasUnknownSource => HasSource(SourceTypeUnknown);

    public bool HasAnySourceDetails => Sources.Count > 0;

    public List<GallerySourceViewModel> SourcesWithDetails
    {
        get
        {
            List<GallerySourceViewModel> withDetails = [];
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
        ? Resources.gallery_item_unknown_source_unsupported_tooltip
        : Resources.gallery_item_unknown_source_unavailable_tooltip;

    public string NoSourceMenuText => Resources.gallery_item_no_source_menu_text;

    public string NoSourceDetailsText => Resources.gallery_item_no_source_details_text;

    public string? WinGetId => GetSource(SourceTypeWinGet)?.Id;

    public string? StoreId => GetSource(SourceTypeStore)?.Id;

    public string? InstallUrl => GetSource(SourceTypeGitHub)?.Uri ?? GetSource(SourceTypeWebsite)?.Uri;

    public string WinGetInstallCommand => !string.IsNullOrWhiteSpace(WinGetId) ? $"winget install --id {WinGetId}" : string.Empty;

    public bool CanCopyWinGetInstallCommand => !string.IsNullOrWhiteSpace(WinGetInstallCommand);

    public string WinGetTooltip => !string.IsNullOrWhiteSpace(WinGetId)
        ? FormatResource(Resources.gallery_item_winget_tooltip_with_id, WinGetId)
        : Resources.gallery_item_winget_tooltip;

    public string StoreTooltip => !string.IsNullOrWhiteSpace(StoreId)
        ? FormatResource(Resources.gallery_item_store_tooltip_with_id, StoreId)
        : Resources.gallery_item_store_tooltip;

    public string GitHubTooltip => GetSource(SourceTypeGitHub)?.Uri ?? Resources.gallery_item_github_source;

    public string WebsiteTooltip => GetSource(SourceTypeWebsite)?.Uri ?? Homepage ?? Resources.gallery_item_website_source;

    public string WinGetMenuText => !string.IsNullOrWhiteSpace(WinGetId)
        ? FormatResource(Resources.gallery_item_winget_menu_text_with_id, WinGetId)
        : Resources.gallery_item_winget_menu_text;

    public string StoreMenuText => !string.IsNullOrWhiteSpace(StoreId)
        ? FormatResource(Resources.gallery_item_store_menu_text_with_id, StoreId)
        : Resources.gallery_item_store_menu_text;

    public string GitHubMenuText => Resources.gallery_item_github_source;

    public string WebsiteMenuText => Resources.gallery_item_website_source;

    public string? PackageFamilyName => _entry.Detection?.PackageFamilyName;

    public bool IsWinGetAvailable => _winGetPackageManagerService?.State.IsAvailable ?? false;

    public string? WinGetUnavailableMessage => HasWinGetSource && !IsWinGetAvailable ? _winGetPackageManagerService?.State.Message : null;

    public bool ShowWinGetUnavailableMessage => !string.IsNullOrWhiteSpace(WinGetUnavailableMessage);

    public bool ShowInstallViaWinGetButton => HasWinGetSource && (!IsInstalled || IsUpdateAvailable);

    public bool CanInstallViaWinGet => ShowInstallViaWinGetButton && IsWinGetAvailable && !IsWinGetActionInProgress;

    public string InstallViaWinGetText => IsUpdateAvailable
        ? Resources.gallery_item_update_action
        : Resources.gallery_item_install_action;

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

    public bool ShowInstallButton => !ShowInstalledBadge;

    public bool ShowUpdateBadge => IsUpdateAvailable;

    public bool ShowWinGetActionIndicator => IsWinGetActionInProgress;

    public bool ShowWinGetActionStatus => IsWinGetActionInProgress && !string.IsNullOrWhiteSpace(WinGetActionMessage);

    public string InstallStatusText =>
        IsUpdateAvailable
            ? Resources.gallery_item_install_status_update_available
            : IsInstalled
                ? Resources.gallery_item_install_status_installed
                : IsInstalledStateKnown
                    ? Resources.gallery_item_install_status_not_installed
                    : Resources.gallery_item_install_status_unavailable;

    public string WinGetStatusText =>
        !HasWinGetSource
            ? string.Empty
            : IsUpdateAvailable
                ? Resources.gallery_item_winget_status_update_available
                : IsInstalled
                    ? Resources.gallery_item_winget_status_installed
                    : IsInstalledStateKnown
                        ? Resources.gallery_item_winget_status_not_installed
                        : Resources.gallery_item_winget_status_unavailable;

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
    private static void OpenInstalledApps()
    {
        ShellHelpers.OpenInShell("ms-settings:appsfeatures");
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
        WinGetActionMessage = IsUpdateAvailable
            ? Resources.gallery_item_winget_action_updating
            : Resources.gallery_item_winget_action_installing;

        try
        {
            var packagesResult = await _winGetPackageManagerService.GetPackagesByIdAsync([WinGetId], includeStoreCatalog: false);
            if (!packagesResult.IsSuccess)
            {
                WinGetActionMessage = packagesResult.ErrorMessage ?? Resources.gallery_item_winget_action_resolve_failed;
                return;
            }

            if (packagesResult.Value is null || !packagesResult.Value.TryGetValue(WinGetId, out var package))
            {
                WinGetActionMessage = Resources.gallery_item_winget_action_package_not_found;
                return;
            }

            var installResult = await _winGetPackageManagerService.InstallPackageAsync(package, skipDependencies: true);
            if (!installResult.Succeeded)
            {
                WinGetActionMessage = installResult.ErrorMessage ?? Resources.gallery_item_winget_action_install_failed;
            }
        }
        catch (Exception ex)
        {
            LogWinGetInstallFailed(_logger, ex);
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
                    ? Resources.gallery_item_winget_action_queued_uninstall
                    : treatAsUpdate
                        ? Resources.gallery_item_winget_action_queued_update
                        : Resources.gallery_item_winget_action_queued_install;
                break;
            case WinGetPackageOperationState.Downloading:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = !operation.ProgressPercent.HasValue;
                WinGetActionProgressValue = operation.ProgressPercent ?? 0;
                WinGetActionMessage = operation.ProgressPercent is uint progressPercent
                    ? FormatResource(Resources.gallery_item_winget_action_downloading_with_progress, progressPercent)
                    : Resources.gallery_item_winget_action_downloading;
                break;
            case WinGetPackageOperationState.Installing:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = treatAsUpdate
                    ? Resources.gallery_item_winget_action_updating
                    : Resources.gallery_item_winget_action_installing;
                break;
            case WinGetPackageOperationState.Uninstalling:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = Resources.gallery_item_winget_action_uninstalling;
                break;
            case WinGetPackageOperationState.PostProcessing:
                IsWinGetActionInProgress = true;
                IsWinGetActionIndeterminate = true;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = Resources.gallery_item_winget_action_finishing;
                break;
            case WinGetPackageOperationState.Succeeded:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 100;
                WinGetActionMessage = operation.Kind == WinGetPackageOperationKind.Uninstall
                    ? Resources.gallery_item_winget_action_succeeded_uninstall
                    : treatAsUpdate
                        ? Resources.gallery_item_winget_action_succeeded_update
                        : Resources.gallery_item_winget_action_succeeded_install;
                ApplyOptimisticTrackedCompletion(operation.Kind);
                break;
            case WinGetPackageOperationState.Canceled:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = Resources.gallery_item_winget_action_canceled;
                break;
            case WinGetPackageOperationState.Failed:
                IsWinGetActionInProgress = false;
                IsWinGetActionIndeterminate = false;
                WinGetActionProgressValue = 0;
                WinGetActionMessage = operation.ErrorMessage ?? Resources.gallery_item_winget_action_failed;
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

    private static bool IsSupportedIconUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ExtensionGalleryScreenshotViewModel> BuildScreenshots(List<string>? screenshotUrls)
    {
        if (screenshotUrls is null || screenshotUrls.Count == 0)
        {
            return [];
        }

        List<ExtensionGalleryScreenshotViewModel> screenshots = [];
        HashSet<string> seenUris = new(OrdinalIgnoreCase);
        for (var i = 0; i < screenshotUrls.Count; i++)
        {
            var screenshotUrl = ToNullIfWhiteSpace(screenshotUrls[i]);
            if (screenshotUrl is null
                || !Uri.TryCreate(screenshotUrl, UriKind.Absolute, out var screenshotUri)
                || !IsSupportedIconUri(screenshotUri)
                || !seenUris.Add(screenshotUri.AbsoluteUri))
            {
                continue;
            }

            screenshots.Add(new ExtensionGalleryScreenshotViewModel(screenshotUri, screenshots.Count));
        }

        return screenshots;
    }

    private GallerySourceViewModel? GetSource(string sourceKind)
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

    private static (IReadOnlyList<GallerySourceViewModel> SourceList, IReadOnlyDictionary<string, GallerySourceViewModel> SourceByKind) BuildSourceInfos(
        IReadOnlyDictionary<string, GalleryInstallSource> installSourcesByType,
        string? homepage)
    {
        Dictionary<string, GallerySourceViewModel> sourcesByKind = new(OrdinalIgnoreCase);

        foreach (var installSource in installSourcesByType.Values)
        {
            var source = CreateSourceFromInstallSource(installSource);
            if (source is null)
            {
                continue;
            }

            UpsertSource(sourcesByKind, source);
        }

        if (TryCreateSourceFromUri(homepage, out var homepageSource))
        {
            UpsertSource(sourcesByKind, homepageSource);
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

    private static void UpsertSource(IDictionary<string, GallerySourceViewModel> sourcesByKind, GallerySourceViewModel source)
    {
        if (sourcesByKind.TryGetValue(source.Kind, out var existing))
        {
            sourcesByKind[source.Kind] = MergeSource(existing, source);
            return;
        }

        sourcesByKind[source.Kind] = source;
    }

    private static GallerySourceViewModel MergeSource(GallerySourceViewModel existing, GallerySourceViewModel incoming)
    {
        return new GallerySourceViewModel(
            existing.Kind,
            existing.DisplayName,
            !string.IsNullOrWhiteSpace(existing.Id) ? existing.Id : incoming.Id,
            !string.IsNullOrWhiteSpace(existing.Uri) ? existing.Uri : incoming.Uri,
            existing.IsKnown || incoming.IsKnown);
    }

    private static GallerySourceViewModel CreateSourceViewModel(
        string kind,
        string displayName,
        string? id,
        string? uri,
        bool isKnown)
    {
        return new GallerySourceViewModel(
            kind,
            displayName,
            id,
            uri,
            isKnown);
    }

    private static GallerySourceViewModel? CreateSourceFromInstallSource(GalleryInstallSource installSource)
    {
        var normalizedType = NormalizeSourceType(installSource.Type);
        if (normalizedType is null)
        {
            return null;
        }

        return normalizedType switch
        {
            SourceTypeWinGet => CreateSourceViewModel(
                SourceTypeWinGet,
                Resources.gallery_item_source_name_winget,
                installSource.Id,
                uri: null,
                isKnown: true),
            SourceTypeStore => CreateSourceViewModel(
                SourceTypeStore,
                Resources.gallery_item_source_name_store,
                installSource.Id,
                uri: null,
                isKnown: true),
            SourceTypeUrl => CreateSourceFromUrl(installSource.Uri),
            _ => CreateSourceViewModel(
                SourceTypeUnknown,
                FormatResource(Resources.gallery_item_source_name_unknown, normalizedType),
                installSource.Id,
                installSource.Uri,
                isKnown: false),
        };
    }

    private static GallerySourceViewModel CreateSourceFromUrl(string? url)
    {
        if (IsGitHubUri(url))
        {
            return CreateSourceViewModel(
                SourceTypeGitHub,
                Resources.gallery_item_source_name_github,
                id: null,
                uri: url,
                isKnown: true);
        }

        return CreateSourceViewModel(
            SourceTypeWebsite,
            Resources.gallery_item_source_name_website,
            id: null,
            uri: url,
            isKnown: true);
    }

    private static bool TryCreateSourceFromUri(string? uriValue, out GallerySourceViewModel source)
    {
        source = default!;
        if (string.IsNullOrWhiteSpace(uriValue) || !Uri.TryCreate(uriValue, UriKind.Absolute, out _))
        {
            return false;
        }

        source = CreateSourceFromUrl(uriValue);
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

    private void ApplySourceDetails(string sourceKind, IReadOnlyList<GallerySourceDetailItemViewModel> details)
    {
        if (!_sourcesByKind.TryGetValue(sourceKind, out var source))
        {
            return;
        }

        source.SetDetails(details);
        OnPropertyChanged(nameof(SourcesWithDetails));
        OnPropertyChanged(nameof(HasSourceMetadataDetails));
    }

    private static List<GallerySourceDetailItemViewModel> CreateSourceDetails(WinGetPackageDetails details)
    {
        List<GallerySourceDetailItemViewModel> rows = [];

        AddDetail(rows, Resources.gallery_source_detail_summary_label, details.Summary, uri: null);
        AddDetail(rows, Resources.gallery_source_detail_description_label, details.Description, uri: null);
        AddDetail(rows, Resources.gallery_source_detail_version_label, details.Version, uri: null);
        AddDetail(rows, Resources.gallery_source_detail_package_label, details.Name, uri: null);
        AddDetail(rows, Resources.gallery_source_detail_publisher_label, details.Publisher, details.PublisherUrl);
        AddDetail(rows, Resources.gallery_source_detail_author_label, details.Author, uri: null);
        AddDetail(rows, Resources.gallery_source_detail_license_label, details.License, details.LicenseUrl);
        AddDetail(rows, Resources.gallery_source_detail_support_label, null, details.PublisherSupportUrl);
        AddDetail(rows, Resources.gallery_source_detail_package_page_label, null, details.PackageUrl);
        AddDetail(rows, Resources.gallery_source_detail_release_notes_label, details.ReleaseNotes, details.ReleaseNotesUrl);

        for (var i = 0; i < details.DocumentationLinks.Count; i++)
        {
            var link = details.DocumentationLinks[i];
            AddDetail(rows, link.Label, null, link.Url);
        }

        AddDetail(rows, Resources.gallery_source_detail_tags_label, BuildTagsText(details.Tags), uri: null);

        return rows;
    }

    private static void AddDetail(ICollection<GallerySourceDetailItemViewModel> target, string label, string? value, string? uri)
    {
        var normalizedValue = ToNullIfWhiteSpace(value);
        var normalizedUri = TryCreateUri(uri);
        if (normalizedValue is null && normalizedUri is null)
        {
            return;
        }

        target.Add(new GallerySourceDetailItemViewModel(label, normalizedValue ?? normalizedUri!.AbsoluteUri, normalizedUri));
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

    private static string FormatResource(string format, params object?[] args)
    {
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args);
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

    private ImageSource CreateImageSource(Uri iconUri)
    {
        try
        {
            return new BitmapImage(iconUri);
        }
        catch (Exception ex)
        {
            LogIconLoadFailed(_logger, iconUri.AbsoluteUri, ex);
            return new BitmapImage(PlaceholderIconUri);
        }
    }

    private static void LogWinGetInstallFailed(ILogger logger, Exception exception)
    {
        LogWinGetInstallFailedMessage(logger, exception);
    }

    private static void LogIconLoadFailed(ILogger logger, string iconUri, Exception exception)
    {
        LogIconLoadFailedMessage(logger, iconUri, exception);
    }

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstalledBadge));
        OnPropertyChanged(nameof(ShowInstallButton));
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
        OnPropertyChanged(nameof(ShowInstallButton));
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
