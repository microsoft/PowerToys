// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Attempts to convert Command Palette protocol activations into typed application routes.
/// </summary>
/// <remarks>
/// This service is the boundary between untrusted <c>x-cmdpal:</c> input and application dispatch.
/// Implementations must validate the complete route, return <see langword="false"/> for unsupported
/// or structurally invalid URIs, and perform no activation side effects. A returned route describes
/// the requested action only; it does not authorize execution or imply user consent. Callers remain
/// responsible for applying policy and dispatching the route.
/// </remarks>
public interface ICmdPalProtocolActivation
{
    /// <summary>
    /// Attempts to parse a protocol activation URI.
    /// </summary>
    /// <param name="uri">The URI supplied by protocol activation, treated as untrusted input.</param>
    /// <param name="route">
    /// When this method returns <see langword="true"/>, contains the parsed route; otherwise,
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="uri"/> is a supported, structurally valid
    /// <c>x-cmdpal:</c> URI; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryParse(Uri? uri, [NotNullWhen(true)] out CmdPalProtocolRoute? route);
}
