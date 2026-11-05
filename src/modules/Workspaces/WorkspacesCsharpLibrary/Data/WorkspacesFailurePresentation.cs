// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesCsharpLibrary.Data;

public sealed record WorkspacesFailurePresentation(string MessageKey, string RetryKind, bool CanSetup, bool CanRetry)
{
    public static WorkspacesFailurePresentation For(string code, bool allowSetup, bool allowRetry)
    {
        string message = code switch
        {
            "NotProvisioned" => "ProtectedStorageNotProvisioned",
            "ServiceUnavailable" => "ProtectedStorageServiceUnavailable",
            "LaunchFailed" => "ProtectedStorageLaunchFailed",
            "LaunchBusy" => "ProtectedStorageLaunchBusy",
            "CaptureFailed" => "ProtectedStorageCaptureFailed",
            "ConnectionClosed" => "ProtectedStorageConnectionClosed",
            "Timeout" => "ProtectedStorageTimedOut",
            "Unauthorized" or "TargetDenied" => "ProtectedStorageAccessDenied",
            "RevisionConflict" => "ProtectedStorageConflict",
            "OutcomeUnknown" => "ProtectedStorageUnknown",
            "CleanupPending" => "ProtectedStorageCleanupPending",
            "OwnerContextRequired" => "ProtectedStorageRunNormally",
            "IncompatibleVersion" => "ProtectedStorageIncompatible",
            "SetupRestartRequired" => "ProtectedStorageRestart",
            "SetupOutcomeUnknown" => "ProtectedStorageSetupUnknown",
            "InvalidPayload" => "ProtectedStorageInvalid",
            _ => "ProtectedStorageUnavailable",
        };
        string retry = code switch
        {
            "OutcomeUnknown" or "SetupOutcomeUnknown" => "InspectUnknownOutcome",
            "SetupRestartRequired" => "RetryAfterRestart",
            "ServiceUnavailable" => "RetryAfterServiceRecovery",
            "Unauthorized" or "TargetDenied" => "RetryAfterComponentRecovery",
            "NotProvisioned" or "OwnerContextRequired" or "SetupAuthorizationRequired" => "RetryAfterAuthorization",
            "InvalidPayload" or "SetupFailed" => "RetryAfterInputFix",
            _ => "RetryNow",
        };
        return new(
            message,
            retry,
            allowSetup && code is "NotProvisioned" or "AuthorizationRequired" or "RecoveryRequired" or "SetupAuthorizationRequired",
            allowRetry && retry is not ("RetryAfterRestart" or "RetryAfterAuthorization" or "RetryAfterServiceRecovery" or "RetryAfterComponentRecovery"));
    }
}
