// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconSourceProvider
{
    Task<IconSource?> GetIconSource(IconDataViewModel icon, double scale);
}
