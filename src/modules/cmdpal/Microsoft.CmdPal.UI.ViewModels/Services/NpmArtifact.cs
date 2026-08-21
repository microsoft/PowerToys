// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// The reason an <see cref="NpmArtifact"/> failed validation. The installer maps each value to a
/// localized message. Keeping the reason as an enum keeps validation free of UI concerns and gives
/// tests one exact failure to assert.
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

    /// <summary>The version is not an exact semantic version (a range or dist tag was supplied).</summary>
    VersionInvalid,

    /// <summary>The integrity value is missing.</summary>
    IntegrityMissing,

    /// <summary>The integrity value is not a supported Subresource Integrity (sha512) hash.</summary>
    IntegrityInvalid,

    /// <summary>The registry is present but is not an approved canonical HTTPS origin.</summary>
    RegistryInvalid,
}

/// <summary>
/// A validated description of the npm package the gallery may install. Instances only come from
/// <see cref="TryCreate"/>, so callers get npm grammar, an exact version, a sha512 Subresource
/// Integrity value, and an approved HTTPS registry when one is supplied. The install spec is the
/// literal "name@version", not a flag, path, URL, git ref, or tarball.
/// </summary>
public sealed class NpmArtifact
{
    // The default public npm registry. If the catalog omits a registry, npm uses this host
    // implicitly. If it specifies one, the host must be on this allowlist.
    private static readonly HashSet<string> ApprovedRegistryHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "registry.npmjs.org",
    };

    // npm package name grammar: optional @scope/ prefix, then a name segment. Each segment starts
    // with an ASCII letter or digit and otherwise allows only letters, digits, '.', '_', and '-'.
    // This blocks whitespace, path separators, ':', '#', and a leading '-' or '@' in the name
    // segment, so the value cannot become a path, URL, git ref, or flag.
    private static readonly Regex PackageNameRegex = new(
        "^(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Exact semantic version: major.minor.patch with optional prerelease and build metadata. Range
    // operators (^, ~, >, <, =, *, x, ||, -) and dist tags such as "latest" do not match, so only one
    // concrete version is accepted.
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

    /// <summary>Gets the approved canonical registry origin ("https://host/"), or null to use npm's machine default.</summary>
    public string? Registry { get; }

    /// <summary>
    /// Gets the exact npm install spec, always the literal "name@version". Because both halves were
    /// validated, this can never be interpreted by npm as anything other than a registry package at a
    /// single version.
    /// </summary>
    public string InstallSpec => $"{Package}@{Version}";

    /// <summary>
    /// Validates the parts of an approved artifact and returns an immutable <see cref="NpmArtifact"/>
    /// when they pass. Any missing or malformed part fails closed with a specific
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
            if (!TryCanonicalizeRegistry(trimmedRegistry, out normalizedRegistry))
            {
                error = NpmArtifactValidationError.RegistryInvalid;
                return false;
            }
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

    private static bool TryCanonicalizeRegistry(string registry, out string? canonical)
    {
        canonical = null;

        if (!Uri.TryCreate(registry, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // npm later receives this value as the "--registry" argument. Keep it to a canonical origin
        // so userinfo, ports, paths, query strings, fragments, and shell metacharacters cannot ride
        // along. Store a rebuilt value from the host instead of echoing the caller's raw string.
        var pathIsRoot = uri.AbsolutePath.Length == 0 || uri.AbsolutePath == "/";
        var isCanonicalOrigin =
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && ApprovedRegistryHosts.Contains(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && pathIsRoot;

        if (!isCanonicalOrigin)
        {
            return false;
        }

        canonical = $"https://{uri.Host}/";
        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="url"/> is an absolute HTTPS URL served by an approved
    /// registry host. Unlike <see cref="TryCanonicalizeRegistry"/>, this accepts package paths from
    /// lockfiles (for example, "https://registry.npmjs.org/left-pad/-/left-pad-1.3.0.tgz"). The
    /// lockfile check uses it to reject file:, git:, http:, and any host outside the allowlist.
    /// </summary>
    internal static bool IsRegistrySourcedHttps(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && ApprovedRegistryHosts.Contains(uri.Host);
    }

    /// <summary>
    /// Determines whether <paramref name="integrity"/> is a supported sha512 Subresource Integrity
    /// value. The lockfile check uses it to reject dependencies without an integrity hash.
    /// </summary>
    internal static bool IsSupportedIntegrity(string? integrity) =>
        !string.IsNullOrWhiteSpace(integrity) && IntegrityRegex.IsMatch(integrity.Trim());
}
