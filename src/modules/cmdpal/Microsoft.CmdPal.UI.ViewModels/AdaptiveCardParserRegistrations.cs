// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class AdaptiveCardParserRegistrations
{
    public static AdaptiveElementParserRegistration ElementParsers { get; } = new();

    public static AdaptiveActionParserRegistration ActionParsers { get; } = new();
}
