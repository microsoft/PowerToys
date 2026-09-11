// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.Models
{
    public sealed class SignatureWarningRequest
    {
        public string RequestId { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Title => Resources.SignatureWarningTitle;

        public string Explanation => Reason.StartsWith("package-", StringComparison.Ordinal)
            ? Resources.SignatureWarningPackageExplanation
            : Resources.SignatureWarningExplanation;

        public string TrustLimits => Resources.SignatureWarningTrustLimits;

        public string AppLabel => Resources.SignatureWarningApp;

        public string PathLabel => Resources.SignatureWarningPath;

        public string ReasonLabel => Resources.SignatureWarningReason;

        public string DetailsLabel => Resources.SignatureWarningDetails;

        public string ArgumentsLabel => Resources.SignatureWarningArguments;

        public string StatusLabel => Resources.SignatureWarningStatus;

        public string SkipText => Resources.SignatureWarningSkip;

        public string SkipHelp => Resources.SignatureWarningSkipHelp;

        public string RunAnywayText => Resources.SignatureWarningRunAnyway;

        public string RunAnywayHelp => Resources.SignatureWarningRunAnywayHelp;

        public string CopyDetailsButtonText => Resources.SignatureWarningCopyDetails;

        public string CopyDetailsErrorText => Resources.SignatureWarningCopyDetailsError;

        public string DetailsText => string.Join(
            Environment.NewLine,
            AppLabel,
            AppName,
            string.Empty,
            PathLabel,
            Path,
            string.Empty,
            ReasonLabel,
            ReasonText,
            string.Empty,
            ArgumentsLabel,
            Arguments,
            string.Empty,
            StatusLabel,
            Status);

        public string ReasonText => Reason switch
        {
            "unsigned" => Resources.SignatureWarningUnsigned,
            "invalid-signature" => Resources.SignatureWarningInvalid,
            "certificate-untrusted" => Resources.SignatureWarningCertificate,
            "revoked" => Resources.SignatureWarningRevoked,
            "explicit-distrust" => Resources.SignatureWarningDistrusted,
            "expired" => Resources.SignatureWarningExpired,
            "revocation-unavailable" => Resources.SignatureWarningRevocationUnavailable,
            "unresolved-target" => Resources.SignatureWarningUnresolvedTarget,
            "package-not-found" => Resources.SignatureWarningPackageNotFound,
            "package-development" => Resources.SignatureWarningPackageDevelopment,
            "package-external-content" => Resources.SignatureWarningPackageExternalContent,
            "package-integrity-failed" => Resources.SignatureWarningPackageIntegrity,
            "package-unavailable" => Resources.SignatureWarningPackageUnavailable,
            "package-unsigned" => Resources.SignatureWarningPackageUnsigned,
            "package-signing-policy" => Resources.SignatureWarningPackageSigningPolicy,
            "package-verification-unavailable" => Resources.SignatureWarningPackageVerificationUnavailable,
            "package-changed" => Resources.SignatureWarningPackageChanged,
            _ => Resources.SignatureWarningUnavailable,
        };
    }
}
