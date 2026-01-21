// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel : IDisposable,
    IRecipient<CommandsReloadedMessage>,
    IPageContext,
    INotifyPropertyChanged
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly UISettings _uiSettings;

    private DockSettings _settings;
    private Color _currentSystemAccentColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public ObservableCollection<TopLevelViewModel> AllItems => _topLevelCommandManager.DockBands;

    // Background image properties for BlurImageControl binding
    public ImageSource? BackgroundImageSource { get; private set; }

    public Stretch BackgroundImageStretch { get; private set; } = Stretch.UniformToFill;

    public double BackgroundImageOpacity { get; private set; }

    public Color BackgroundImageTint { get; private set; }

    public double BackgroundImageTintIntensity { get; private set; }

    public int BackgroundImageBlurAmount { get; private set; }

    public double BackgroundImageBrightness { get; private set; }

    public bool ShowBackgroundImage { get; private set; }

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        SettingsModel settings,
        TaskScheduler scheduler)
    {
        _topLevelCommandManager = tlcManager;
        _settings = settings.DockSettings;
        Scheduler = scheduler;
        WeakReferenceMessenger.Default.Register<CommandsReloadedMessage>(this);

        _uiSettings = new UISettings();
        UpdateAccentColor(_uiSettings);
    }

    public void UpdateSettings(DockSettings settings)
    {
        Logger.LogDebug($"DockViewModel.UpdateSettings");
        _settings = settings;
        SetupBands();
        UpdateBackgroundImageProperties();
    }

    private void UpdateAccentColor(UISettings sender)
    {
        _currentSystemAccentColor = sender.GetColorValue(UIColorType.Accent);
    }

    private void UpdateBackgroundImageProperties()
    {
        // Determine effective theme color based on colorization mode
        var effectiveThemeColor = _settings.ColorizationMode switch
        {
            ColorizationMode.WindowsAccentColor => _currentSystemAccentColor,
            ColorizationMode.CustomColor or ColorizationMode.Image => _settings.CustomThemeColor,
            _ => Colors.Transparent,
        };

        // Determine if we should show a background image
        var hasBackgroundImage = _settings.ColorizationMode == ColorizationMode.Image
            && !string.IsNullOrWhiteSpace(_settings.BackgroundImagePath);

        ImageSource? imageSource = null;
        if (hasBackgroundImage && Uri.TryCreate(_settings.BackgroundImagePath, UriKind.RelativeOrAbsolute, out var uri))
        {
            imageSource = new BitmapImage(uri);
        }

        BackgroundImageSource = imageSource;
        BackgroundImageStretch = _settings.BackgroundImageFit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            _ => Stretch.UniformToFill,
        };
        BackgroundImageOpacity = _settings.BackgroundImageOpacity / 100.0;
        BackgroundImageTint = effectiveThemeColor;
        BackgroundImageTintIntensity = _settings.CustomThemeColorIntensity / 100.0;
        BackgroundImageBlurAmount = _settings.BackgroundImageBlurAmount;
        BackgroundImageBrightness = _settings.BackgroundImageBrightness / 100.0;
        ShowBackgroundImage = imageSource != null;

        // Notify property changes
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageSource)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageStretch)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageOpacity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageTint)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageTintIntensity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageBlurAmount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageBrightness)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowBackgroundImage)));
    }

    private void SetupBands()
    {
        Logger.LogDebug($"Setting up dock bands");
        SetupBands(_settings.StartBands, StartItems);
        SetupBands(_settings.EndBands, EndItems);
    }

    private void SetupBands(
        List<DockBandSettings> bands,
        ObservableCollection<DockBandViewModel> target)
    {
        List<DockBandViewModel> newBands = new();
        foreach (var band in bands)
        {
            var commandId = band.Id;
            var topLevelCommand = _topLevelCommandManager.LookupDockBand(commandId);

            if (topLevelCommand is null)
            {
                Logger.LogWarning($"Failed to find band {commandId}");
            }

            if (topLevelCommand is not null)
            {
                var bandVm = CreateBandItem(band, topLevelCommand.ItemViewModel);
                newBands.Add(bandVm);
            }
        }

        var beforeCount = target.Count;
        var afterCount = newBands.Count;

        DoOnUiThread(() =>
        {
            ListHelpers.InPlaceUpdateList(target, newBands, out var removed);
            var isStartBand = target == StartItems;
            var label = isStartBand ? "Start bands:" : "End bands:";
            Logger.LogDebug($"{label} ({beforeCount}) -> ({afterCount}), Removed {removed?.Count ?? 0} items");
        });
    }

    public void Dispose()
    {
    }

    public void Receive(CommandsReloadedMessage message)
    {
        SetupBands();
        CoreLogger.LogDebug("Bands reloaded");
    }

    private DockBandViewModel CreateBandItem(
        DockBandSettings bandSettings,
        CommandItemViewModel commandItem)
    {
        DockBandViewModel band = new(commandItem, new(this), bandSettings, _settings);
        band.InitializeProperties(); // TODO! make async
        return band;
    }

    public DockBandViewModel? FindBandByTopLevel(TopLevelViewModel tlc)
    {
        var id = tlc.Id;
        return FindBandById(id);
    }

    public DockBandViewModel? FindBandById(string id)
    {
        foreach (var band in StartItems)
        {
            if (band.Id == id)
            {
                return band;
            }
        }

        foreach (var band in EndItems)
        {
            if (band.Id == id)
            {
                return band;
            }
        }

        return null;
    }

    public void ShowException(Exception ex, string? extensionHint = null)
    {
        var extensionText = extensionHint ?? "<unknown>";
        CoreLogger.LogError($"Error in extension {extensionText}", ex);
    }

    private void DoOnUiThread(Action action)
    {
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
    }

    public CommandItemViewModel GetContextMenuForDock()
    {
        var model = new DockContextMenuItem();
        var vm = new CommandItemViewModel(new(model), new(this));
        vm.SlowInitializeProperties();
        return vm;
    }

    private sealed partial class DockContextMenuItem : CommandItem
    {
        public DockContextMenuItem()
        {
            var openSettingsCommand = new AnonymousCommand(
                action: () =>
                {
                    WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Dock"));
                })
            {
                Name = "Customize", // TODO!Loc
                Icon = Icons.SettingsIcon,
            };

            MoreCommands = new CommandContextItem[]
            {
                new CommandContextItem(openSettingsCommand),
            };
        }
    }
}

#pragma warning restore SA1402 // File may only contain a single type
