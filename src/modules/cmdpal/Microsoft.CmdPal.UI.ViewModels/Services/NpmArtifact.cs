// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// The reason an <see cref="NpmArtifact"/> failed validation. The installer maps each value to a
/// localized, user-facing message; keeping the reason as an enum keeps validation free of any
/// presentation concern and lets tests assert the exact failure.
/// </summary>
public enum NpmArtifactValidationError
{
    /// <summary>No error; the artifact is valid.</summary>
    None,

    /// <summary>The package name is missing.</summary>
    PackageMissing,

    /// <summary>The package name is not a valid npm package name.</summary>
    PackageInvalid,

    /// <summary>The version is missing.</summary>
    VersionMissing,

    /// <summary>The version is not an exact semantic version (a range or dist-tag was supplied).</summary>
    VersionInvalid,

    /// <summary>The integrity value is missing.</summary>
    IntegrityMissing,

    /// <summary>The integrity value is not a supported Subresource Integrity (sha512) hash.</summary>
    IntegrityInvalid,

    /// <summary>The registry is present but is not an approved absolute HTTPS URL.</summary>
    RegistryInvalid,
}

/// <summary>
/// An immutable, validated description of the npm artifact the gallery is allowed to install. The
/// only way to obtain one is through <see cref="TryCreate"/>, so any instance is guaranteed to carry
/// a package name that matches the npm grammar, an exact version (never a range or dist-tag), a
/// sha512 Subresource Integrity value, and, when present, an approved HTTPS registry. The npm install
/// spec is always the literal "name@version" and can never be read as a flag, path, URL, git ref, or
/// tarball.
/// </summary>
public sealed class NpmArtifact
{
    // The default public npm registry. When a catalog entry omits a registry this host is used
    // implicitly by npm; when it specifies one it must be on this allowlist.
    private static readonly HashSet<string> ApprovedRegistryHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "registry.npmjs.org",
    };

    // npm package name grammar (RFC-ish): an optional @scope/ prefix, then a name segment. Each
    // segment starts with an ASCII letter or digit and otherwise allows only letters, digits, and
    // the '.', '_', '-' characters. This forbids whitespace, path separators, ':', '#', and a
    // leading '-' or '@' in the name segment, so the value can never be a path, URL, git ref, or flag.
    private static readonly Regex PackageNameRegex = new(
        "^(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Exact semantic version: major.minor.patch with optional pre-release and build metadata. Range
    // operators (^, ~, >, <, =, *, x, ||, -) and dist-tags (such as "latest") do not match, so only a
    // single concrete version is ever accepted.
    private static readonly Regex ExactVersionRegex = new(
        @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?(?:\+[0-9A-Za-z][0-9A-Za-z.-]*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Subresource Integrity for a sha512 digest: the "sha512-" prefix followed by standard base64.
    private static readonly Regex IntegrityRegex = new(
        "^sha512-[A-Za-z0-9+/]+={0,2}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int MaxPackageNameLength = 214;

    private NpmArtifact(string package, string version, string integrity, string? registry)
    {
        Package = package;
        Version = version;
        Integrity = integrity;
        Registry = registry;
    }

    /// <summary>Gets the validated npm package name.</summary>
    public string Package { get; }

    /// <summary>Gets the exact version to install.</summary>
    public string Version { get; }

    /// <summary>Gets the sha512 Subresource Integrity value of the approved tarball.</summary>
    public string Integrity { get; }

    /// <summary>Gets the approved registry URL, or null to use the machine default.</summary>
    public string? Registry { get; }

    /// <summary>
    /// Gets the exact npm install spec, always the literal "name@version". Because both halves were
    /// validated, this can never be interpreted by npm as anything other than a registry package at a
    /// single version.
    /// </summary>
    public string InstallSpec => $"{Package}@{Version}";

    /// <summary>
    /// Validates the parts of an approved artifact and, on success, returns an immutable
    /// <see cref="NpmArtifact"/>. Fails closed: any missing or malformed part yields a specific
    /// <see cref="NpmArtifactValidationError"/> and no artifact.
    /// </summary>
    /// <param name="package">The npm package name.</param>
    /// <param name="version">The exact version.</param>
    /// <param name="integrity">The sha512 Subresource Integrity value.</param>
    /// <param name="registry">The optional registry URL.</param>
    /// <param name="artifact">The validated artifact when the method returns true; otherwise null.</param>
    /// <param name="error">The reason validation failed when the method returns false; otherwise <see cref="NpmArtifactValidationError.None"/>.</param>
    /// <returns><see langword="true"/> when the artifact is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(
        string? package,
        string? version,
        string? integrity,
        string? registry,
        out NpmArtifact? artifact,
        out NpmArtifactValidationError error)
    {
        artifact = null;

        var trimmedPackage = package?.Trim() ?? string.Empty;
        if (trimmedPackage.Length == 0)
        {
            error = NpmArtifactValidationError.PackageMissing;
            return false;
        }

        if (trimmedPackage.Length > MaxPackageNameLength || !PackageNameRegex.IsMatch(trimmedPackage))
        {
            error = NpmArtifactValidationError.PackageInvalid;
            return false;
        }

        var trimmedVersion = version?.Trim() ?? string.Empty;
        if (trimmedVersion.Length == 0)
        {
            error = NpmArtifactValidationError.VersionMissing;
            return false;
        }

        if (!ExactVersionRegex.IsMatch(trimmedVersion))
        {
            error = NpmArtifactValidationError.VersionInvalid;
            return false;
        }

        var trimmedIntegrity = integrity?.Trim() ?? string.Empty;
        if (trimmedIntegrity.Length == 0)
        {
            error = NpmArtifactValidationError.IntegrityMissing;
            return false;
        }

        if (!IntegrityRegex.IsMatch(trimmedIntegrity))
        {
            error = NpmArtifactValidationError.IntegrityInvalid;
            return false;
        }

        string? normalizedRegistry = null;
        var trimmedRegistry = registry?.Trim();
        if (!string.IsNullOrEmpty(trimmedRegistry))
        {
            if (!IsApprovedRegistry(trimmedRegistry))
            {
                error = NpmArtifactValidationError.RegistryInvalid;
                return false;
            }

            normalizedRegistry = trimmedRegistry;
        }

        // Defense in depth: the join must not resolve to a flag even if the regexes ever loosen.
        var spec = $"{trimmedPackage}@{trimmedVersion}";
        if (spec.StartsWith('-'))
        {
            error = NpmArtifactValidationError.PackageInvalid;
            return false;
        }

        artifact = new NpmArtifact(trimmedPackage, trimmedVersion, trimmedIntegrity, normalizedRegistry);
        error = NpmArtifactValidationError.None;
        return true;
    }

    private static bool IsApprovedRegistry(string registry)
    {
        if (!Uri.TryCreate(registry, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && ApprovedRegistryHosts.Contains(uri.Host);
    }
}
