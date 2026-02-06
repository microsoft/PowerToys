// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

public record ShowCommandInContextMenuMessage(IContextMenuContext Context, Point Position);
