// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal enum ShellIconDiagnosticStep
{
    // These values are emitted in the ShellIconStepCompleted ETW payload. Preserve
    // existing values and append new steps so saved traces remain decodable.
    Request = 0,
    LocationCacheHit = 1,
    LocationCacheMiss = 2,
    RawInFlightJoin = 3,
    IdentityResolved = 4,
    CanonicalCacheHit = 5,
    CanonicalInFlightJoin = 6,
    CanonicalNewLoad = 7,
    ExtractionSucceeded = 8,
    ExtractionEmpty = 9,
    ExtractionFailed = 10,
    AssociationChangedNotification = 11,
    LocationCacheInvalidated = 12,
    TypeFallbackSucceeded = 13,
    TypeFallbackEmpty = 14,
    TypeFallbackFailed = 15,
    IntermediateDispatchAccepted = 16,
    IntermediateDispatchRejected = 17,
    ExactRefinementSame = 18,
    ExactRefinementDifferent = 19,
    ExactRefinementFailed = 20,
    IntermediatePresentationApplied = 21,
    IntermediatePresentationSkipped = 22,
}
