// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A ready-to-use <see cref="IAuthorizationResult"/> implementation. Command
/// Palette produces this on the host side; the Toolkit type is provided for
/// symmetry and testing.
/// </summary>
public partial class AuthorizationResult : IAuthorizationResult
{
    public bool IsSuccessful { get; set; }

    public string RedirectUri { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> ResponseParameters { get; set; } = new Dictionary<string, string>();

    public string Error { get; set; } = string.Empty;
}
