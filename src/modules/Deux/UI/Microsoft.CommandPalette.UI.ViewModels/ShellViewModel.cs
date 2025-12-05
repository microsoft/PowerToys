// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Microsoft.CommandPalette.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ILogger logger;
    private readonly TaskScheduler _scheduler;

    [ObservableProperty]
    public partial bool IsLoaded { get; set; } = false;

    private bool _isNested;

    public bool IsNested => _isNested;

    public ShellViewModel(
        TaskScheduler scheduler,
        ILogger logger)
    {
        this.logger = logger;
        _scheduler = scheduler;
        _isNested = false;
    }

    public void GoHome(bool withAnimation = true, bool focusSearch = true)
    {
    }

    public void GoBack(bool withAnimation = true, bool focusSearch = true)
    {
    }

    private void OnUIThread(Action action)
    {
        _ = Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            _scheduler);
    }
}
