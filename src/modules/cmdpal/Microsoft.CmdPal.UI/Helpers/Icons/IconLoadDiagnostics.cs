// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Opt-in, process-local measurements for the CmdPal icon pipeline.
/// No icon strings, paths, glyphs, application identifiers, or item data are recorded.
/// Diagnostic scopes are static developer-authored labels.
/// </summary>
internal static class IconLoadDiagnostics
{
    private static readonly object ReportsLock = new();
    private static readonly List<IconLoadDiagnosticsReport> Reports = [];
    private static long _nextSessionId;
    private static IconLoadDiagnosticsSession? _activeSession;

    public static bool IsRecording => Volatile.Read(ref _activeSession) is not null;

    public static long? ActiveSessionId => Volatile.Read(ref _activeSession)?.Id;

    public static IReadOnlyList<IconLoadDiagnosticsReport> GetReports()
    {
        lock (ReportsLock)
        {
            return Reports.ToArray();
        }
    }

    public static long Start(DispatcherQueue? dispatcherQueue = null)
    {
        var session = new IconLoadDiagnosticsSession(
            Interlocked.Increment(ref _nextSessionId),
            dispatcherQueue);
        Interlocked.Exchange(ref _activeSession, session)?.Stop();
        return session.Id;
    }

    public static IconLoadDiagnosticsReport? StopAndCreateReport()
    {
        var session = Interlocked.Exchange(ref _activeSession, null);
        if (session is null)
        {
            return null;
        }

        session.Stop();
        var report = session.CreateReport();
        lock (ReportsLock)
        {
            Reports.Add(report);
        }

        Logger.LogInfo(report.Text);

        return report;
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _activeSession, null)?.Stop();
        lock (ReportsLock)
        {
            Reports.Clear();
        }
    }

    public static IconRequestMeasurement BeginRequest(IconRequestReason reason, double scale)
    {
        return BeginRequest(reason, scale, default);
    }

    public static IconRequestMeasurement BeginRequest(IconRequestReason reason, double scale, IconRequestOrigin origin)
    {
        var session = Volatile.Read(ref _activeSession);
        return session is null
            ? default
            : session.BeginRequest(reason, scale, origin);
    }

    public static IconLoadMeasurement? CreateLoad(
        IconRequestMeasurement request,
        string? iconString,
        bool hasStream,
        double width,
        double height,
        double scale)
    {
        var session = request.Session ?? Volatile.Read(ref _activeSession);
        if (session is null || !ReferenceEquals(session, Volatile.Read(ref _activeSession)))
        {
            return null;
        }

        return session.CreateLoad(ClassifyInput(iconString, hasStream), width, height, scale);
    }

    public static long BeginElementUpdate()
    {
        return Volatile.Read(ref _activeSession) is null ? 0 : Stopwatch.GetTimestamp();
    }

    public static void RecordElementUpdate(bool reused, IconSource? source, long startedAt)
    {
        var session = Volatile.Read(ref _activeSession);
        if (session is null)
        {
            return;
        }

        var elapsedTicks = startedAt == 0 ? -1 : Stopwatch.GetTimestamp() - startedAt;
        session.RecordElementUpdate(reused, ClassifyResult(source), elapsedTicks);
    }

    private static IconLoadInputKind ClassifyInput(string? iconString, bool hasStream)
    {
        if (!string.IsNullOrEmpty(iconString))
        {
            var path = iconString.AsSpan();
            var comma = path.IndexOf(',');
            if (comma >= 0)
            {
                path = path[..comma];
            }

            if (path.EndsWith(".exe", StringComparison.Ordinal)
                || path.EndsWith(".dll", StringComparison.Ordinal)
                || path.EndsWith(".lnk", StringComparison.Ordinal))
            {
                return IconLoadInputKind.ShellBinary;
            }

            return IconLoadInputKind.String;
        }

        return hasStream ? IconLoadInputKind.Stream : IconLoadInputKind.Empty;
    }

    internal static IconLoadResultKind ClassifyResult(IconSource? result)
    {
        try
        {
            return result switch
            {
                null => IconLoadResultKind.Empty,
                FontIconSource font when font.FontFamily?.Source.StartsWith("Segoe UI Emoji", StringComparison.OrdinalIgnoreCase) == true => IconLoadResultKind.EmojiGlyph,
                FontIconSource font when font.FontFamily?.Source.StartsWith("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) == true => IconLoadResultKind.FluentGlyph,
                FontIconSource => IconLoadResultKind.OtherGlyph,
                ImageIconSource { ImageSource: SvgImageSource } => IconLoadResultKind.Svg,
                ImageIconSource { ImageSource: SoftwareBitmapSource } => IconLoadResultKind.SoftwareBitmap,
                ImageIconSource { ImageSource: BitmapImage } => IconLoadResultKind.Bitmap,
                BitmapIconSource => IconLoadResultKind.Fallback,
                _ => IconLoadResultKind.Other,
            };
        }
        catch
        {
            return IconLoadResultKind.Other;
        }
    }
}
