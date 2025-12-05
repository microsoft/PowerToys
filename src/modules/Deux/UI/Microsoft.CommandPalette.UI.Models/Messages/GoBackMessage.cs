// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.UI.Models.Messages;

public record GoBackMessage(bool WithAnimation = true, bool FocusSearch = true)
{
    // TODO! sticking these properties here feels like leaking the UI into the models
}
