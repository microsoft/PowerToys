// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.ViewModels.Messages;

// TODO! sticking these properties here feels like leaking the UI into the models
public record GoHomeMessage(bool WithAnimation = true, bool FocusSearch = true);
