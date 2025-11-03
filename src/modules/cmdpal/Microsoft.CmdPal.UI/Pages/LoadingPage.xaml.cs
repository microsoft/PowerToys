// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Pages;

/// <summary>
/// We use this page to do initialization of our extensions and cache loading to hydrate our ViewModels.
/// </summary>
public sealed partial class LoadingPage : Page
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public LoadingPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest request)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (request.TargetViewModel is not ShellViewModel shellVM)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ShellViewModel)}");
        }

        // This will load the built-in commands, then navigate to the main page.
        // Once the mainpage loads, we'll start loading extensions.
        shellVM.LoadCommand.Execute(null);

        _ = Task.Run(async () =>
        {
            await shellVM.LoadCommand.ExecutionTask!;

            if (shellVM.LoadCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
            {
                // TODO: Handle failure case
            }
        });

        base.OnNavigatedTo(e);
    }
}
