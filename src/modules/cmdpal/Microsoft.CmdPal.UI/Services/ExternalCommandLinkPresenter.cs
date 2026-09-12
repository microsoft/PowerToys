// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Services;

internal sealed partial class ExternalCommandLinkPresenter : IExternalCommandLinkPresenter
{
    private readonly ShellContentDialogHost _dialogHost;
    private readonly ICmdPalProtocolActivation _protocolActivation;
    private readonly IMessenger _messenger;
    private readonly string _reloadConsentDescription;
    private readonly string _commandConsentDescription;
    private readonly string _pageConsentDescription;
    private readonly string _originWarningDescription;
    private readonly string _pageStateLabel;
    private readonly string _showMoreButtonText;
    private readonly string _showLessButtonText;
    private readonly string _loadingTitle;
    private readonly string _loadingDescription;
    private readonly CompositeFormat _linkDescription;
    private readonly CompositeFormat _pageFilterDescription;
    private readonly CompositeFormat _pageQueryDescription;

    internal ExternalCommandLinkPresenter(
        ShellContentDialogHost dialogHost,
        ICmdPalProtocolActivation protocolActivation,
        IMessenger messenger)
    {
        _dialogHost = dialogHost;
        _protocolActivation = protocolActivation;
        _messenger = messenger;
        _reloadConsentDescription = ResourceLoaderInstance.GetString("ExternalCommandLink_ReloadConsent_Description");
        _commandConsentDescription = ResourceLoaderInstance.GetString("ExternalCommandLink_CommandConsent_Description");
        _pageConsentDescription = ResourceLoaderInstance.GetString("ExternalCommandLink_PageConsent_Description");
        _originWarningDescription = ResourceLoaderInstance.GetString("ExternalCommandLink_OriginWarning_Description");
        _pageStateLabel = ResourceLoaderInstance.GetString("ExternalCommandLink_PageState_Label");
        _showMoreButtonText = ResourceLoaderInstance.GetString("ExternalCommandLink_ShowMore_Button");
        _showLessButtonText = ResourceLoaderInstance.GetString("ExternalCommandLink_ShowLess_Button");
        _loadingTitle = ResourceLoaderInstance.GetString("ExternalCommandLink_Loading_Title");
        _loadingDescription = ResourceLoaderInstance.GetString("ExternalCommandLink_Loading_Description");
        _linkDescription = CompositeFormat.Parse(ResourceLoaderInstance.GetString("ExternalCommandLink_Link_Description"));
        _pageFilterDescription = CompositeFormat.Parse(ResourceLoaderInstance.GetString("ExternalCommandLink_PageFilter_Description"));
        _pageQueryDescription = CompositeFormat.Parse(ResourceLoaderInstance.GetString("ExternalCommandLink_PageQuery_Description"));
    }

    public async Task<IExternalCommandLinkDialogSession?> PrepareLoadingAsync()
    {
        SummonWindow();
        if (!await _dialogHost.WaitForXamlRootAsync())
        {
            return null;
        }

        var lease = await _dialogHost.AcquireAsync();
        if (lease is null)
        {
            return null;
        }

        try
        {
            var dialog = CreateDialog();
            ConfigureLoadingDialog(dialog);
            return new ExternalCommandLinkDialogSession(this, lease, dialog);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public async Task<ExternalCommandConsentResult> RequestConsentAsync(
        ExternalCommandConsentRequest request,
        bool summonWindow = true)
    {
        if (summonWindow)
        {
            SummonWindow();
        }

        if (!await _dialogHost.WaitForXamlRootAsync())
        {
            return ExternalCommandConsentResult.Rejected;
        }

        var dialog = CreateDialog();
        ConfigureConsentDialog(dialog, request);

        using var lease = await _dialogHost.AcquireAsync(enterDialogMode: true);
        if (lease is null)
        {
            return ExternalCommandConsentResult.Rejected;
        }

        return ToConsentResult(await ShowContentDialogAsync(dialog));
    }

    public async Task ShowUnavailableAsync(bool summonWindow = true)
    {
        if (summonWindow)
        {
            SummonWindow();
        }

        if (!await _dialogHost.WaitForXamlRootAsync())
        {
            return;
        }

        var dialog = CreateDialog();
        ConfigureUnavailableDialog(dialog);

        using var lease = await _dialogHost.AcquireAsync(enterDialogMode: true);
        if (lease is not null)
        {
            _ = await ShowContentDialogAsync(dialog);
        }
    }

    private void SummonWindow() => _messenger.Send(new ShowWindowMessage(IntPtr.Zero));

    private ContentDialog CreateDialog()
    {
        var dialog = new ContentDialog
        {
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _dialogHost.XamlRoot,
        };

        dialog.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((_, args) =>
            {
                if (args.Key == VirtualKey.Escape)
                {
                    args.Handled = true;
                    dialog.Hide();
                }
            }),
            handledEventsToo: true);
        return dialog;
    }

    private void ConfigureConsentDialog(ContentDialog dialog, ExternalCommandConsentRequest request)
    {
        var link = _protocolActivation.CreateUri(request.Route).AbsoluteUri;
        dialog.Title = ResourceLoaderInstance.GetString("ExternalCommandLink_Consent_Title");
        dialog.Content = CreateConsentContent(link, request);
        dialog.PrimaryButtonText = ResourceLoaderInstance.GetString(
            request.IsReload ? "ExternalCommandLink_ReloadOnce_Button" :
            request.IsPage ? "ExternalCommandLink_OpenOnce_Button" : "ExternalCommandLink_RunOnce_Button");
        dialog.SecondaryButtonText = request.CanRemember
            ? ResourceLoaderInstance.GetString("ExternalCommandLink_AlwaysAllow_Button")
            : string.Empty;
        dialog.CloseButtonText = ResourceLoaderInstance.GetString("ExternalCommandLink_Cancel_Button");
        dialog.DefaultButton = ContentDialogButton.Close;
    }

    private void ConfigureLoadingDialog(ContentDialog dialog)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        content.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock
        {
            Text = _loadingDescription,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        dialog.Title = _loadingTitle;
        dialog.Content = content;
        dialog.PrimaryButtonText = string.Empty;
        dialog.SecondaryButtonText = string.Empty;
        dialog.CloseButtonText = ResourceLoaderInstance.GetString("ExternalCommandLink_Cancel_Button");
        dialog.DefaultButton = ContentDialogButton.Close;
    }

    private static void ConfigureUnavailableDialog(ContentDialog dialog)
    {
        dialog.Title = ResourceLoaderInstance.GetString("ExternalCommandLink_Unavailable_Title");
        dialog.Content = ResourceLoaderInstance.GetString("ExternalCommandLink_Unavailable_Description");
        dialog.PrimaryButtonText = string.Empty;
        dialog.SecondaryButtonText = string.Empty;
        dialog.CloseButtonText = ResourceLoaderInstance.GetString("ExternalCommandLink_Close_Button");
        dialog.DefaultButton = ContentDialogButton.Close;
    }

    private FrameworkElement CreateConsentContent(string link, ExternalCommandConsentRequest request)
    {
        var content = new StackPanel { Spacing = 12 };
        var requestDescription = request.IsReload
            ? _reloadConsentDescription
            : request.ListPageOptions is null
                ? _commandConsentDescription
                : _pageConsentDescription;
        content.Children.Add(CreateConsentText(requestDescription));

        var preview = new CommandPreviewBox();
        var icon = request.Icon ?? (request.IsReload ? CreateReloadExtensionsIcon() : null);
        preview.Configure(request.Permission.CommandName, request.Permission.ProviderName, icon);
        content.Children.Add(preview);

        if (request.ListPageOptions is not null)
        {
            var state = new List<string>(3);
            if (request.ListPageOptions.FilterId is { } filterId)
            {
                state.Add(string.Format(CultureInfo.CurrentCulture, _pageFilterDescription, filterId));
            }

            if (request.ListPageOptions.Query is { } query)
            {
                state.Add(string.Format(CultureInfo.CurrentCulture, _pageQueryDescription, query));
            }

            if (request.ListPageOptions.RequiresOneTimeConsent)
            {
                state.Add(ResourceLoaderInstance.GetString("ExternalCommandLink_FilterOneTime_Description"));
            }

            content.Children.Add(CreateConsentText(
                $"{_pageStateLabel}{Environment.NewLine}{string.Join(Environment.NewLine, state)}",
                isTextSelectionEnabled: true));
        }

        var originWarning = CreateConsentText(_originWarningDescription);
        if (_dialogHost.TryGetResource<Style>("ExternalCommandLinkWarningTextBlockStyle") is { } warningStyle)
        {
            originWarning.Style = warningStyle;
        }

        content.Children.Add(originWarning);
        content.Children.Add(CreateLinkDisclosure(link));
        return content;
    }

    private FrameworkElement CreateLinkDisclosure(string link)
    {
        var linkText = CreateConsentText(
            string.Format(CultureInfo.CurrentCulture, _linkDescription, link),
            isTextSelectionEnabled: true);
        linkText.Visibility = Visibility.Collapsed;

        var toggle = new HyperlinkButton
        {
            Content = _showMoreButtonText,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = default,
        };

        var isExpanded = false;
        toggle.Click += (_, _) =>
        {
            isExpanded = !isExpanded;
            linkText.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = isExpanded ? _showLessButtonText : _showMoreButtonText;
        };

        var disclosure = new StackPanel { Spacing = 4 };
        disclosure.Children.Add(toggle);
        disclosure.Children.Add(linkText);
        return disclosure;
    }

    private static TextBlock CreateConsentText(string text, bool isTextSelectionEnabled = false)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = isTextSelectionEnabled,
        };
    }

    private static IconInfoViewModel CreateReloadExtensionsIcon()
    {
        var reloadCommand = new ReloadExtensionsCommand();
        var icon = new IconInfoViewModel(reloadCommand.Icon);

        icon.InitializeProperties();
        return icon;
    }

    private static ExternalCommandConsentResult ToConsentResult(ContentDialogResult result) => result switch
    {
        ContentDialogResult.Primary => ExternalCommandConsentResult.AllowOnce,
        ContentDialogResult.Secondary => ExternalCommandConsentResult.AlwaysAllow,
        _ => ExternalCommandConsentResult.Rejected,
    };

    private static async Task<ContentDialogResult> ShowContentDialogAsync(ContentDialog dialog) => await dialog.ShowAsync();

    private sealed partial class ExternalCommandLinkDialogSession : IExternalCommandLinkDialogSession
    {
        private readonly ExternalCommandLinkPresenter _presenter;
        private readonly ShellContentDialogHost.ContentDialogLease _lease;
        private readonly ContentDialog _dialog;
        private Task<ContentDialogResult>? _dialogResultTask;
        private bool _isDisposed;

        internal ExternalCommandLinkDialogSession(
            ExternalCommandLinkPresenter presenter,
            ShellContentDialogHost.ContentDialogLease lease,
            ContentDialog dialog)
        {
            _presenter = presenter;
            _lease = lease;
            _dialog = dialog;
        }

        public Task ShowLoadingAsync()
        {
            return ShowDialogAsync();
        }

        public async Task<ExternalCommandConsentResult> RequestConsentAsync(ExternalCommandConsentRequest request)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_dialogResultTask?.IsCompleted != true)
            {
                _presenter.ConfigureConsentDialog(_dialog, request);
            }

            return ToConsentResult(await ShowDialogAsync());
        }

        public async Task ShowUnavailableAsync()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_dialogResultTask?.IsCompleted != true)
            {
                ConfigureUnavailableDialog(_dialog);
            }

            _ = await ShowDialogAsync();
        }

        public async Task CloseAsync()
        {
            if (_dialogResultTask is null)
            {
                return;
            }

            if (!_dialogResultTask.IsCompleted)
            {
                _dialog.Hide();
            }

            _ = await _dialogResultTask;
        }

        private Task<ContentDialogResult> ShowDialogAsync()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_dialogResultTask is null)
            {
                _lease.EnterDialogMode();
                _dialogResultTask = ShowContentDialogAsync(_dialog);
            }

            return _dialogResultTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                await CloseAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to close the external command link dialog.", ex);
            }
            finally
            {
                _lease.Dispose();
            }
        }
    }
}
