// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RobocopyUI.Converters;
using RobocopyUI.Helpers;
using RobocopyUI.Services.AI;

namespace RobocopyUI.Controls
{
    /// <summary>
    /// Dialog that turns a natural-language description into a validated robocopy command.
    /// </summary>
    public sealed partial class AICommandDialog : ContentDialog
    {
        /// <summary>
        /// Upper bound for a single request. Local models can take a while to start up on the first
        /// call, so this is generous, but it must be bounded so the dialog can never hang forever.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

        private readonly List<AIChatTurn> _conversation = [];
        private readonly ServiceTypeToIconConverter _iconConverter = new();
        private readonly IReadOnlyList<RobocopyOptionDescriptor> _catalog;
        private readonly string _currentSource;
        private readonly string _currentDestination;

        // Held as a delegate rather than a CancellationTokenSource field so the dialog does not
        // become an owner of a disposable (CA1001); the source itself stays scoped to the request.
        private Action? _cancelInFlightRequest;

        private bool _isBusy;
        private bool _isClosed;
        private bool _cancelledByUser;

        // Set while the picker is being populated so programmatic selection is not mistaken for the
        // user choosing a provider.
        private bool _syncingProviderSelection;

        private string _selectedProviderId = string.Empty;

        /// <summary>
        /// Gets the plan the user accepted, or null when the dialog was dismissed.
        /// </summary>
        public RobocopyPlan? AcceptedPlan { get; private set; }

        public AICommandDialog(IReadOnlyList<RobocopyOptionDescriptor> catalog, string currentSource, string currentDestination)
        {
            _catalog = catalog;
            _currentSource = currentSource;
            _currentDestination = currentDestination;

            InitializeComponent();

            IsPrimaryButtonEnabled = false;

            Prompt.PlaceholderText = ResourceLoaderInstance.ResourceLoader.GetString("AIPromptTextBox_Placeholder");
            Prompt.SetInputLabel(PromptLabel);

            LoadProviders();

            if (string.IsNullOrEmpty(_selectedProviderId))
            {
                NotConfiguredInfoBar.IsOpen = true;
                Prompt.IsEnabled = false;
            }
        }

        /// <summary>
        /// Fills the picker with the providers configured for Advanced Paste that Robocopy UI can
        /// actually run, and preselects the active one.
        /// </summary>
        private void LoadProviders()
        {
            var providers = new AIProviderResolver().GetSupportedProviders(out var activeProviderId);

            _syncingProviderSelection = true;

            try
            {
                AIProviderListView.ItemsSource = providers;

                var selected = providers.FirstOrDefault(provider => string.Equals(provider.Id, activeProviderId, StringComparison.OrdinalIgnoreCase))
                    ?? (providers.Count > 0 ? providers[0] : null);

                AIProviderListView.SelectedItem = selected;
                ApplySelectedProvider(selected);

                var hasProviders = providers.Count > 0;
                AIProviderListView.Visibility = hasProviders ? Visibility.Visible : Visibility.Collapsed;
                AIProvidersEmptyText.Visibility = hasProviders ? Visibility.Collapsed : Visibility.Visible;

                // With a single provider there is nothing to switch between, so the picker is only
                // noise next to the prompt.
                AIProviderButton.Visibility = providers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _syncingProviderSelection = false;
            }
        }

        private void ApplySelectedProvider(PasteAIProviderDefinition? provider)
        {
            _selectedProviderId = provider?.Id ?? string.Empty;

            AIProviderIcon.Source = provider is null
                ? null
                : _iconConverter.Convert(provider.ServiceType, typeof(ImageSource), parameter: null!, language: string.Empty) as ImageSource;

            ToolTipService.SetToolTip(AIProviderButton, provider?.DisplayName);
        }

        private void AIProviderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingProviderSelection)
            {
                return;
            }

            if (AIProviderListView.SelectedItem is not PasteAIProviderDefinition provider)
            {
                return;
            }

            if (string.Equals(provider.Id, _selectedProviderId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplySelectedProvider(provider);

            // Advanced Paste and Robocopy UI share one active provider, so the choice made here is
            // the same choice made there.
            new AIProviderResolver().SetActiveProvider(provider.Id);

            AIProviderButton.Flyout?.Hide();
        }

        private async void Prompt_PromptSubmitted(object? sender, EventArgs e)
        {
            await GenerateAsync();
        }

        private void Prompt_RequestCancelled(object? sender, EventArgs e)
        {
            // Cancels the request but leaves the dialog open so the user can edit and retry.
            _cancelledByUser = true;
            _cancelInFlightRequest?.Invoke();
        }

        /// <summary>
        /// Handles every way the dialog can close - the buttons, Esc, and a programmatic hide - so an
        /// in-flight request is always cancelled rather than left running against a dead dialog.
        /// </summary>
        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                AcceptedPlan = null;
            }

            _isClosed = true;
            _cancelInFlightRequest?.Invoke();
        }

        private async Task GenerateAsync()
        {
            if (_isBusy || string.IsNullOrWhiteSpace(Prompt.Text))
            {
                return;
            }

            var userText = Prompt.Text.Trim();
            _conversation.Add(new AIChatTurn(true, userText));

            SetBusy(true);
            ErrorInfoBar.IsOpen = false;
            Prompt.HasError = false;

            using var cancellationTokenSource = new CancellationTokenSource(RequestTimeout);
            _cancelInFlightRequest = cancellationTokenSource.Cancel;

            try
            {
                var cancellationToken = cancellationTokenSource.Token;
                var providerId = _selectedProviderId;

                // Resolving the provider reads settings and the credential vault, and for local
                // models it starts the inference runtime. All of that blocks, so keep it off the
                // UI thread or the whole window stops responding.
                var plan = await Task.Run(
                    async () =>
                    {
                        var resolver = new AIProviderResolver();
                        if (!resolver.TryResolve(providerId, out var config, out _) || config is null)
                        {
                            throw new AIGenerationException(ResourceLoaderInstance.ResourceLoader.GetString("AIError_NotConfigured"));
                        }

                        var generator = new RobocopyCommandGenerator(AIProviderResolver.CreateProvider(config), _catalog);
                        return await generator.GenerateAsync(_conversation, _currentSource, _currentDestination, cancellationToken).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(true);

                if (_isClosed)
                {
                    return;
                }

                if (plan.NeedsFollowUp)
                {
                    // Keep the model's question in the conversation so the next answer has context.
                    _conversation.Add(new AIChatTurn(false, plan.FollowUpQuestion));
                    ShowFollowUp(plan.FollowUpQuestion);
                }
                else
                {
                    _conversation.Add(new AIChatTurn(false, plan.CommandLine));
                    ShowPreview(plan);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is either the user dismissing the dialog, the user pressing the
                // prompt's cancel button, or the request timing out. Only the last is an error.
                if (!_isClosed && !_cancelledByUser)
                {
                    // ShowError drops the unanswered turn and restores its text.
                    ShowError(ResourceLoaderInstance.ResourceLoader.GetString("AIError_Timeout"));
                }
                else
                {
                    // The unanswered turn must not linger, otherwise the next request repeats it.
                    var abandonedText = RemoveLastTurn();

                    if (_cancelledByUser && !string.IsNullOrEmpty(abandonedText))
                    {
                        Prompt.Text = abandonedText;
                    }
                }
            }
            catch (AIGenerationException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to generate robocopy command", ex);
                ShowError(ResourceLoaderInstance.ResourceLoader.GetString("AIError_Generic"));
            }
            finally
            {
                _cancelInFlightRequest = null;
                _cancelledByUser = false;

                if (!_isClosed)
                {
                    SetBusy(false);
                }
            }
        }

        private void ShowFollowUp(string question)
        {
            AcceptedPlan = null;
            IsPrimaryButtonEnabled = false;
            PreviewPanel.Visibility = Visibility.Collapsed;

            FollowUpText.Text = question;
            FollowUpText.Visibility = Visibility.Visible;

            // The question takes the place of the static label rather than stacking above it.
            PromptLabel.Visibility = Visibility.Collapsed;
            Prompt.SetInputLabel(FollowUpText);

            // The starter example is no longer useful once the model is asking a direct question.
            PromptHintText.Visibility = Visibility.Collapsed;

            Prompt.Text = string.Empty;
            Prompt.PlaceholderText = ResourceLoaderInstance.ResourceLoader.GetString("AIPromptTextBox_FollowUpPlaceholder");
            Prompt.FocusInput();
        }

        private void ShowPreview(RobocopyPlan plan)
        {
            AcceptedPlan = plan;
            IsPrimaryButtonEnabled = true;

            FollowUpText.Visibility = Visibility.Collapsed;
            PromptLabel.Visibility = Visibility.Visible;
            Prompt.SetInputLabel(PromptLabel);
            PromptHintText.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Visible;

            PreviewCommandTextBox.Text = plan.CommandLine;
            PreviewExplanationText.Text = plan.Explanation;
            PreviewExplanationText.Visibility = string.IsNullOrWhiteSpace(plan.Explanation) ? Visibility.Collapsed : Visibility.Visible;

            if (plan.Warnings.Count > 0)
            {
                WarningInfoBar.Title = ResourceLoaderInstance.ResourceLoader.GetString("AIWarningsTitle");
                WarningInfoBar.Message = string.Join(Environment.NewLine, plan.Warnings);
                WarningInfoBar.IsOpen = true;
            }
            else
            {
                WarningInfoBar.IsOpen = false;
            }

            // Leave the prompt empty so the user can immediately describe a refinement.
            Prompt.Text = string.Empty;
            Prompt.PlaceholderText = ResourceLoaderInstance.ResourceLoader.GetString("AIPromptTextBox_RefinePlaceholder");
        }

        private void ShowError(string message)
        {
            // The failed turn must not stay in the conversation, otherwise the retry repeats it.
            var failedText = RemoveLastTurn();

            // Put the text back so a retry does not mean retyping it.
            if (!string.IsNullOrEmpty(failedText))
            {
                Prompt.Text = failedText;
            }

            Prompt.HasError = true;

            ErrorInfoBar.Title = ResourceLoaderInstance.ResourceLoader.GetString("AIErrorTitle");
            ErrorInfoBar.Message = message;
            ErrorInfoBar.IsOpen = true;

            Prompt.FocusInput();
        }

        /// <summary>
        /// Drops the most recent turn and returns its text, so a failed or abandoned request does not
        /// stay in the conversation sent to the model.
        /// </summary>
        private string RemoveLastTurn()
        {
            if (_conversation.Count == 0)
            {
                return string.Empty;
            }

            var last = _conversation[^1];
            _conversation.RemoveAt(_conversation.Count - 1);
            return last.IsUser ? last.Text : string.Empty;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            Prompt.IsBusy = busy;

            // While a refinement is running the preview on screen is about to be replaced, so applying
            // it would commit a plan the user has already asked to change.
            IsPrimaryButtonEnabled = !busy && AcceptedPlan is not null;
        }
    }
}
