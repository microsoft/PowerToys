// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels;

internal sealed partial class NullPageViewModel : PageViewModel
{
    internal NullPageViewModel(TaskScheduler scheduler, AppExtensionHost extensionHost)
        : base(null, scheduler, extensionHost)
    {
        HasBackButton = false;
    }
}
