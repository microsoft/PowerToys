// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerDisplay.Contracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
using Windows.Foundation;

namespace PowerToysExtension.Pages;

internal sealed partial class PowerDisplayProfilesPage : ListPage, INotifyItemsChanged
{
    private static readonly CompositeFormat MonitorCountFormat = CompositeFormat.Parse(Resources.PowerDisplay_Profile_MonitorCount_Format);
    private static readonly CompositeFormat ModifiedFormat = CompositeFormat.Parse(Resources.PowerDisplay_Profile_Modified_Format);

    private readonly object _stateLock = new();
    private readonly IPowerDisplayCliService _cliService;
    private readonly IconInfo _icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png");
    private readonly AnonymousCommand _refreshCommand;
    private readonly CommandItem _noProfilesContent;

    private TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChanged;
    private IListItem[] _items = [];
    private CancellationTokenSource? _activeLoad;
    private Task _loadingTask = Task.CompletedTask;
    private bool _isLoading = true;
    private ICommandItem? _emptyContent;

    internal PowerDisplayProfilesPage()
        : this(new PowerDisplayCliService())
    {
    }

    internal PowerDisplayProfilesPage(IPowerDisplayCliService cliService)
    {
        _cliService = cliService ?? throw new ArgumentNullException(nameof(cliService));

        Icon = _icon;
        Name = Title = Resources.PowerDisplay_Profiles_Title;
        Id = "com.microsoft.powertoys.powerDisplay.profiles";
        _refreshCommand = new AnonymousCommand(Refresh)
        {
            Id = "com.microsoft.powertoys.powerDisplay.profiles.refresh",
            Name = Resources.PowerDisplay_Refresh,
            Result = CommandResult.KeepOpen(),
        };
        _noProfilesContent = CreateNoProfilesContent();
        _emptyContent = _noProfilesContent;
    }

    // CmdPal reads GetItems before subscribing. Start work only after the first
    // observer is attached, and keep repeated GetItems calls within that visit cheap.
    event TypedEventHandler<object, IItemsChangedEventArgs> INotifyItemsChanged.ItemsChanged
    {
        add
        {
            var started = false;
            lock (_stateLock)
            {
                var wasObserved = _itemsChanged is not null;
                _itemsChanged += value;
                if (!wasObserved && _itemsChanged is not null)
                {
                    started = StartLoadingUnderLock();
                }
            }

            if (started)
            {
                NotifyStateChanged();
            }
        }

        remove
        {
            CancellationTokenSource? cancelledLoad = null;
            var stopped = false;
            lock (_stateLock)
            {
                var wasObserved = _itemsChanged is not null;
                _itemsChanged -= value;
                if (wasObserved && _itemsChanged is null)
                {
                    cancelledLoad = _activeLoad;
                    _activeLoad = null;
                    _items = [];
                    _emptyContent = _noProfilesContent;
                    _isLoading = true;
                    stopped = true;
                }
            }

            try
            {
                cancelledLoad?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The request finished and disposed its source after we detached it.
            }

            if (stopped)
            {
                NotifyStateChanged();
            }
        }
    }

    public override bool IsLoading
    {
        get
        {
            lock (_stateLock)
            {
                return _isLoading;
            }
        }

        set
        {
            lock (_stateLock)
            {
                _isLoading = value;
            }

            OnPropertyChanged();
        }
    }

    public override ICommandItem? EmptyContent
    {
        get
        {
            lock (_stateLock)
            {
                return _emptyContent;
            }
        }

        set
        {
            lock (_stateLock)
            {
                _emptyContent = value;
            }

            OnPropertyChanged();
        }
    }

    internal Task LoadingTask
    {
        get
        {
            lock (_stateLock)
            {
                return _loadingTask;
            }
        }
    }

    public override IListItem[] GetItems()
    {
        lock (_stateLock)
        {
            return _items;
        }
    }

    internal static ListItem CreateProfileListItem(CliProfileInfo profile, IPowerDisplayCliService cliService)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(cliService);

        var command = new ApplyPowerDisplayProfileCommand(profile.Id, profile.Name, cliService);
        return new ListItem(command)
        {
            Title = command.Name,
            Subtitle = BuildSubtitle(profile),
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png"),
        };
    }

    private static string BuildSubtitle(CliProfileInfo profile)
    {
        var monitorCount = profile.MonitorCount == 1
            ? Resources.PowerDisplay_Profile_OneMonitor
            : string.Format(CultureInfo.CurrentCulture, MonitorCountFormat, profile.MonitorCount);

        var modified = Resources.PowerDisplay_Profile_ModifiedUnknown;
        if (DateTimeOffset.TryParse(
                profile.LastModified,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var lastModified))
        {
            modified = string.Format(
                CultureInfo.CurrentCulture,
                ModifiedFormat,
                lastModified.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        }

        return $"{monitorCount} · {modified}";
    }

    private void Refresh()
    {
        bool started;
        lock (_stateLock)
        {
            started = StartLoadingUnderLock();
        }

        if (started)
        {
            NotifyStateChanged();
            NotifyItemsChanged();
        }
    }

    private bool StartLoadingUnderLock()
    {
        if (_itemsChanged is null || _activeLoad is not null)
        {
            return false;
        }

        var load = new CancellationTokenSource();
        _activeLoad = load;
        _items = [];
        _emptyContent = _noProfilesContent;
        _isLoading = true;
        _loadingTask = Task.Run(() => LoadProfilesAsync(load));
        return true;
    }

    private async Task LoadProfilesAsync(CancellationTokenSource load)
    {
        try
        {
            PowerDisplayCliResult<CliProfileListResult> result;
            try
            {
                result = await _cliService.GetProfilesAsync(load.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (load.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Loading Power Display profiles failed: {ex}");
                result = PowerDisplayCliResult<CliProfileListResult>.Failure(PowerDisplayCliFailureKind.ProcessFailure);
            }

            IListItem[] items = [];
            try
            {
                if (result.IsSuccess && result.Value is not null)
                {
                    items = result.Value.Profiles.Select(profile =>
                    {
                        var item = CreateProfileListItem(profile, _cliService);
                        item.MoreCommands = [new CommandContextItem(_refreshCommand)];
                        return (IListItem)item;
                    }).ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Creating Power Display profile commands failed: {ex}");
                result = PowerDisplayCliResult<CliProfileListResult>.Failure(PowerDisplayCliFailureKind.ProcessFailure);
                items = [];
            }

            var emptyContent = result.IsSuccess
                ? _noProfilesContent
                : CreateErrorContent(result.FailureKind);
            lock (_stateLock)
            {
                if (!ReferenceEquals(_activeLoad, load))
                {
                    return;
                }

                _items = items;
                _emptyContent = emptyContent;
                _isLoading = false;
                _activeLoad = null;
            }

            NotifyStateChanged();
            NotifyItemsChanged();
        }
        finally
        {
            load.Dispose();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(EmptyContent));
        OnPropertyChanged(nameof(IsLoading));
    }

    private void NotifyItemsChanged()
    {
        TypedEventHandler<object, IItemsChangedEventArgs>? handlers;
        int count;
        lock (_stateLock)
        {
            handlers = _itemsChanged;
            count = _items.Length;
        }

        try
        {
            handlers?.Invoke(this, new ItemsChangedEventArgs(count));
        }
        catch
        {
            // The host may have disconnected after we captured its handlers.
        }
    }

    private CommandItem CreateNoProfilesContent()
    {
        return new CommandItem(new OpenPowerToysSettingsCommand(Resources.PowerDisplay_DisplayName, "PowerDisplay", dismissAfterOpening: true)
        {
            Id = "com.microsoft.powertoys.powerDisplay.openSettings",
        })
        {
            Title = Resources.PowerDisplay_NoProfiles_Title,
            Subtitle = Resources.PowerDisplay_NoProfiles_Subtitle,
            Icon = _icon,
            MoreCommands = [new CommandContextItem(_refreshCommand)],
        };
    }

    private CommandItem CreateErrorContent(PowerDisplayCliFailureKind failureKind)
    {
        var retryCommand = new AnonymousCommand(Refresh)
        {
            Id = "com.microsoft.powertoys.powerDisplay.profiles.retry",
            Name = Resources.PowerDisplay_Retry,
            Result = CommandResult.KeepOpen(),
        };

        return new CommandItem(retryCommand)
        {
            Title = Resources.PowerDisplay_LoadError_Title,
            Subtitle = GetLoadErrorSubtitle(failureKind),
            Icon = _icon,
            MoreCommands = [new CommandContextItem(_refreshCommand)],
        };
    }

    private static string GetLoadErrorSubtitle(PowerDisplayCliFailureKind failureKind) => failureKind switch
    {
        PowerDisplayCliFailureKind.ArgumentError => Resources.PowerDisplay_LoadError_Argument,
        PowerDisplayCliFailureKind.Timeout => Resources.PowerDisplay_LoadError_Timeout,
        PowerDisplayCliFailureKind.InternalError => Resources.PowerDisplay_LoadError_Internal,
        PowerDisplayCliFailureKind.ProviderUnavailable => Resources.PowerDisplay_LoadError_ProviderUnavailable,
        PowerDisplayCliFailureKind.MissingExecutable => Resources.PowerDisplay_LoadError_MissingCli,
        PowerDisplayCliFailureKind.InvalidResponse => Resources.PowerDisplay_LoadError_InvalidResponse,
        PowerDisplayCliFailureKind.Cancelled => Resources.PowerDisplay_LoadError_Cancelled,
        _ => Resources.PowerDisplay_LoadError_Process,
    };
}
