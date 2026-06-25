// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.WinUI.Animations;
using Microsoft.PowerToys.Common.UI.Controls.Window;

namespace PowerAccent.UI;

public sealed partial class Selector : TransparentWindow, IDisposable
{
    private readonly Core.PowerAccent _powerAccent;

    public SelectorViewModel ViewModel { get; } = new();

    public Selector()
    {
        InitializeComponent();

        // No animations: instant show/hide for typing-aid responsiveness.
        ShowAnimations = new ImplicitAnimationSet();
        HideAnimations = new ImplicitAnimationSet();

        _powerAccent = new Core.PowerAccent(action => DispatcherQueue.TryEnqueue(() => action()));
    }

    public void Dispose()
    {
        _powerAccent?.Dispose();
        GC.SuppressFinalize(this);
    }
}
