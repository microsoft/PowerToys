// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels;

internal sealed partial class NullPageViewModel(TaskScheduler scheduler, AppExtensionHost extensionHost)
    : PageViewModel(null, scheduler, extensionHost);
