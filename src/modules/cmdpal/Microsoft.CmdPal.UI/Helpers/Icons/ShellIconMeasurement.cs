// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly struct ShellIconMeasurement
{
    private readonly IconLoadDiagnosticsSession? _session;

    internal ShellIconMeasurement(IconLoadDiagnosticsSession session, ShellIconRequestKind requestKind)
    {
        _session = session;
        session.RecordShellIconStep(ShellIconDiagnosticStep.Request, (int)requestKind, 0);
    }

    private ShellIconMeasurement(IconLoadDiagnosticsSession session)
    {
        _session = session;
    }

    public ShellIconMeasurement CreateSuboperation() =>
        _session is null ? default : new ShellIconMeasurement(_session);

    public bool IsEnabled => _session is not null;

    public void LocationCacheHit() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.LocationCacheHit, 0, 0);

    public void LocationCacheMiss() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.LocationCacheMiss, 0, 0);

    public void RawInFlightJoin() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.RawInFlightJoin, 0, 0);

    public void CanonicalCacheHit() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.CanonicalCacheHit, 0, 0);

    public void CanonicalInFlightJoin() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.CanonicalInFlightJoin, 0, 0);

    public void CanonicalNewLoad() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.CanonicalNewLoad, 0, 0);

    public long BeginTypeFallback() => _session is null ? 0 : Stopwatch.GetTimestamp();

    public void TypeFallbackCompleted(long startedAt, bool hasContent, bool dispatchAccepted)
    {
        _session?.RecordShellIconStep(
            hasContent ? ShellIconDiagnosticStep.TypeFallbackSucceeded : ShellIconDiagnosticStep.TypeFallbackEmpty,
            0,
            ElapsedSince(startedAt));
        if (hasContent)
        {
            _session?.RecordShellIconStep(
                dispatchAccepted ? ShellIconDiagnosticStep.IntermediateDispatchAccepted : ShellIconDiagnosticStep.IntermediateDispatchRejected,
                0,
                0);
        }
    }

    public void IntermediatePresentationCompleted(long startedAt, bool applied) =>
        _session?.RecordShellIconStep(
            applied ? ShellIconDiagnosticStep.IntermediatePresentationApplied : ShellIconDiagnosticStep.IntermediatePresentationSkipped,
            0,
            ElapsedSince(startedAt));

    public void TypeFallbackFailed(long startedAt) =>
        _session?.RecordShellIconStep(
            ShellIconDiagnosticStep.TypeFallbackFailed,
            0,
            ElapsedSince(startedAt));

    public void ExactRefinementCompleted(bool sameSource) =>
        _session?.RecordShellIconStep(
            sameSource ? ShellIconDiagnosticStep.ExactRefinementSame : ShellIconDiagnosticStep.ExactRefinementDifferent,
            0,
            0);

    public void ExactRefinementFailed() =>
        _session?.RecordShellIconStep(ShellIconDiagnosticStep.ExactRefinementFailed, 0, 0);

    public long BeginIdentityResolution() => _session is null ? 0 : Stopwatch.GetTimestamp();

    public void IdentityResolved(ShellIconIdentityKind kind, long startedAt) =>
        _session?.RecordShellIconStep(
            ShellIconDiagnosticStep.IdentityResolved,
            (int)kind,
            ElapsedSince(startedAt));

    public long BeginExtraction() => _session is null ? 0 : Stopwatch.GetTimestamp();

    public void ExtractionCompleted(
        long startedAt,
        ShellIconIdentityKind kind,
        bool hasContent) =>
        _session?.RecordShellIconStep(
            hasContent ? ShellIconDiagnosticStep.ExtractionSucceeded : ShellIconDiagnosticStep.ExtractionEmpty,
            (int)kind,
            ElapsedSince(startedAt));

    public void ExtractionFailed(long startedAt, ShellIconIdentityKind kind) =>
        _session?.RecordShellIconStep(
            ShellIconDiagnosticStep.ExtractionFailed,
            (int)kind,
            ElapsedSince(startedAt));

    public void SystemImageListExtracted(
        ShellImageListSize imageListSize,
        int requestedPixelSize,
        int sourceWidth,
        int sourceHeight,
        long hIconConversionTicks) =>
        _session?.RecordShellImageListExtraction(
            imageListSize,
            requestedPixelSize,
            sourceWidth,
            sourceHeight,
            hIconConversionTicks);

    private static long ElapsedSince(long startedAt) =>
        startedAt == 0 ? 0 : Stopwatch.GetTimestamp() - startedAt;
}
