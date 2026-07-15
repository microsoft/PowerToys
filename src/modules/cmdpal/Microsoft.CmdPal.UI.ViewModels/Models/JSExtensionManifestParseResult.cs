// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// The outcome of attempting to parse and validate a package.json as a CmdPal extension manifest.
/// </summary>
public sealed record JSExtensionManifestParseResult
{
    private JSExtensionManifestParseResult(bool isValid, JSExtensionManifest? manifest, string? failureReason)
    {
        IsValid = isValid;
        Manifest = manifest;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether the package.json is a valid CmdPal extension manifest.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the parsed manifest when <see cref="IsValid"/> is true; otherwise null.
    /// </summary>
    public JSExtensionManifest? Manifest { get; }

    /// <summary>
    /// Gets a human-readable explanation when <see cref="IsValid"/> is false; otherwise null.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="manifest">The validated manifest.</param>
    /// <returns>A successful <see cref="JSExtensionManifestParseResult"/>.</returns>
    public static JSExtensionManifestParseResult Success(JSExtensionManifest manifest)
    {
        return new JSExtensionManifestParseResult(true, manifest, null);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The reason the package.json is not a valid CmdPal extension.</param>
    /// <returns>A failed <see cref="JSExtensionManifestParseResult"/>.</returns>
    public static JSExtensionManifestParseResult Failure(string reason)
    {
        return new JSExtensionManifestParseResult(false, null, reason);
    }
}
