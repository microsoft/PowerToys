// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class FlyoutMenuItem : ModuleListItem
{
    private bool _visible;

    public string ToolTip { get; set; } = string.Empty;

    public new ModuleType Tag
    {
        get => (ModuleType)(base.Tag ?? ModuleType.PowerLauncher);
        set => base.Tag = value;
    }

    public override bool IsEnabled
    {
        get => base.IsEnabled;
        set
        {
            if (base.IsEnabled != value)
            {
                base.IsEnabled = value;
                EnabledChangedCallback?.Invoke(this);
            }
        }
    }

    public Action<FlyoutMenuItem>? EnabledChangedCallback { get; set; }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                OnPropertyChanged();
            }
        }
    }
}
