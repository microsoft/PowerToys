// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;

namespace Microsoft.CmdPal.UI.Views;

public interface ICurrentPageAware
{
    PageViewModel? CurrentPageViewModel { get; set; }
}
