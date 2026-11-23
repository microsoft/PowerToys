// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Represents one UI request's live interest in an icon load.
/// </summary>
/// <remarks>
/// Only unit tests construct this. It keeps <see cref="IconRequestDemandState"/> — the state
/// machine SourceRequestedEventArgs uses in production — testable without linking XAML types
/// into the test project.
/// </remarks>
internal sealed class IconRequestDemand : IIconRequestDemand
{
    private IconRequestDemandState _state;

    public void Attach(IconLoadDemand loadDemand) => _state.Attach(loadDemand);

    public void Release() => _state.Release();
}
