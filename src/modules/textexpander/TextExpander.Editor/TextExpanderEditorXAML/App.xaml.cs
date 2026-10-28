// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Microsoft.PowerToys.TextExpander.Editor;

public partial class App : Application
{
    private WindowEx _window;

    public App()
    {
        string language = LanguageHelper.LoadLanguage();
        if (!string.IsNullOrEmpty(language))
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language;
        }

        InitializeComponent();
        UnhandledException += (_, e) => Logger.LogError("Unhandled Text Expander editor exception.", e.Exception);
    }

    internal static Window EditorWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new WindowEx
        {
            Title = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_EditorTitle"),
            MinWidth = 640,
            MinHeight = 480,
            Width = 1000,
            Height = 800,
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop(),
        };
        EditorWindow = _window;
        _window.Content = new TextExpanderPage();
        _window.Closed += (_, _) => EditorWindow = null;
        ActivateEditor();
    }

    internal void ActivateEditor()
    {
        if (_window is null)
        {
            return;
        }

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.State == OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }

            _window.Activate();
            _window.BringToFront();
        });
    }
}
