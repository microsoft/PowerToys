// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Content;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using PowerOCR.Controls;
using PowerOCR.Core.Models;
using WinUIEx;

namespace PowerOCR.Helpers;

/// <summary>
/// Opt-in, bounded cursor tracing. Input callbacks only append to memory; the sampler
/// writes a separate local trace after the overlay closes or after 60 seconds.
/// </summary>
internal sealed partial class CursorDiagnostics : IDisposable
{
    private const int MaximumEntries = 16000;
    private const int SampleIntervalMilliseconds = 2;
    private readonly object _gate = new();
    private readonly TaskCompletionSource<bool> _cleanupCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<string> _entries = new();
    private readonly List<string> _metadata = new();
    private readonly List<(RoutedEvent Event, PointerEventHandler Handler)> _handlers = new();
    private readonly Dictionary<(nint Hwnd, uint Message), CursorMessageProbe.MessageSample> _lastNative = new();
    private readonly OCROverlay _window;
    private readonly DispatcherQueue _dispatcher;
    private readonly FrameworkElement _root;
    private readonly SelectionCanvas _canvas;
    private readonly DisplayBounds _bounds;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly string _path;
    private readonly nint _cross = LoadCursor(0, 32515);
    private readonly nint _arrow = LoadCursor(0, 32512);
    private CursorMessageProbe? _probe;
    private CursorWinEventProbe? _winEventProbe;
    private DispatcherQueueTimer? _diagnosticTimer;
    private InputPointerSource? _inputSource;
    private InputObservation? _lastInput;
    private ulong _islandId;
    private long _lastHealthCheck;
    private long _lastInputAttempt;
    private long _inputEvents;
    private long _cursorEvents;
    private bool _sawPointer;
    private UiObservation? _lastUi;
    private long _lastPointerRecord;
    private long _nativeMessages;
    private long _nativeCursorChanges;
    private long _overwritten;
    private int _stopRequested;
    private bool _recording = true;
    private bool _disposed;

    private CursorDiagnostics(OCROverlay window, FrameworkElement root, SelectionCanvas canvas, DisplayBounds bounds)
    {
        _window = window;
        _dispatcher = window.DispatcherQueue;
        _root = root;
        _canvas = canvas;
        _bounds = bounds;
        var hwnd = window.GetWindowHandle();
        _path = Path.Combine(
            Logger.CurrentVersionLogDirectoryPath,
            $"CursorTrace_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Environment.ProcessId}_{hwnd:X}.log");
        _metadata.Add(FormattableString.Invariant($"format=2 utc={DateTime.UtcNow:O} pid={Environment.ProcessId} overlay=0x{hwnd:X} display={bounds} qpcStart={_started} qpcFrequency={Stopwatch.Frequency} requestedSampleMs={SampleIntervalMilliseconds} maximumSeconds=60 capacity={MaximumEntries}"));
        _metadata.Add($"window-host systemBackdrop={window.SystemBackdrop?.GetType().Name ?? "none"} extendsTitleBar={window.ExtendsContentIntoTitleBar}");
        _metadata.Add($"app-base {AppContext.BaseDirectory}");
        _metadata.Add($"systemCross=0x{_cross:X} systemArrow=0x{_arrow:X}; cursor labels are handle comparisons, not image recognition.");
        _metadata.Add("sample = global GetCursorInfo; native = UI-thread GetCursor before/after forwarding; lastUi is an earlier routed event, not a simultaneous hit test. Rows can be interleaved; order by tMs/qpc. A missed transient does not prove stability.");
        _metadata.Add("format=2 adds native-probe-health, input-source and cursor-event. WinEvent eventTid identifies the notification source, not a setter stack; its cursor is read later at callback time. Input sources and cursors are borrowed from WinUI and only read.");
    }

    internal static CursorDiagnostics? TryStart(OCROverlay window, FrameworkElement root, SelectionCanvas canvas, DisplayBounds bounds)
    {
        // Checked for every overlay, so enabling/disabling does not require restarting Runner.
        if (!File.Exists(Path.Combine(Logger.AppLogDirectoryPath, "cursor-diagnostics.enabled")))
        {
            return null;
        }

        CursorDiagnostics? diagnostics = null;
        try
        {
            diagnostics = new CursorDiagnostics(window, root, canvas, bounds);
            diagnostics.Start();
            return diagnostics;
        }
        catch (Exception ex)
        {
            diagnostics?.Dispose();
            Logger.LogError("Could not start PowerOCR cursor diagnostics.", ex);
            return null;
        }
    }

    private void Start()
    {
        _probe = new CursorMessageProbe(_window.GetWindowHandle(), RecordWindow, RecordNativeMessage);
        _canvas.Diagnostics = this;
        AddPointerHandler(UIElement.PointerMovedEvent, "move");
        AddPointerHandler(UIElement.PointerEnteredEvent, "enter");
        AddPointerHandler(UIElement.PointerExitedEvent, "exit");
        AddPointerHandler(UIElement.PointerPressedEvent, "press");
        AddPointerHandler(UIElement.PointerReleasedEvent, "release");
        AddPointerHandler(UIElement.PointerCanceledEvent, "cancel");
        AddPointerHandler(UIElement.PointerCaptureLostEvent, "capture-lost");
        _root.Loaded += OnLoaded;
        _window.Activated += OnActivated;
        _window.Closed += OnClosed;
        _probe.Refresh();
        _probe.CheckHealth("start");
        _winEventProbe = new CursorWinEventProbe(message => Record("cursor-event-status", message), RecordCursorEvent);
        _winEventProbe.Start();
        _diagnosticTimer = _dispatcher.CreateTimer();
        _diagnosticTimer.Interval = TimeSpan.FromMilliseconds(100);
        _diagnosticTimer.Tick += OnDiagnosticTick;
        _diagnosticTimer.Start();
        Logger.LogInfo($"PowerOCR cursor diagnostics enabled for up to 60 seconds. Trace will be saved to {_path}");
        new Thread(SampleCursor) { IsBackground = true, Name = "PowerOCR cursor diagnostics" }.Start();
    }

    private void AddPointerHandler(RoutedEvent routedEvent, string name)
    {
        PointerEventHandler handler = (_, e) =>
        {
            try
            {
                RecordPointer(name, e);
            }
            catch (Exception ex)
            {
                Record("pointer-error", ex.ToString());
            }
        };
        _handlers.Add((routedEvent, handler));
        _root.AddHandler(routedEvent, handler, handledEventsToo: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _probe?.Refresh();
        _probe?.CheckHealth("loaded");
        RecordCanvasState("root-loaded");
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        _probe?.Refresh();
        Record("activation", $"state={e.WindowActivationState}");
    }

    private void OnClosed(object sender, WindowEventArgs e) => Dispose();

    internal void RecordCanvasState(string reason)
    {
        try
        {
            Record("canvas", FormattableString.Invariant($"reason={reason} crossAssigned={_canvas.HasCrossCursor} loaded={_canvas.IsLoaded} sizeDip=({_canvas.ActualWidth:F2},{_canvas.ActualHeight:F2}) scale={_canvas.XamlRoot?.RasterizationScale:F2} captures={_canvas.PointerCaptures?.Count ?? 0}"));
        }
        catch (Exception ex)
        {
            Record("canvas-error", ex.ToString());
        }
    }

    private void RecordPointer(string name, PointerRoutedEventArgs e)
    {
        if (Volatile.Read(ref _stopRequested) != 0 || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        var point = e.GetCurrentPoint(_root);
        var source = e.OriginalSource;
        if (!_sawPointer)
        {
            _sawPointer = true;
            _probe?.Refresh();
            _probe?.CheckHealth("first-pointer");
        }

        AttachInputSource();
        RecordInputState("xaml-pointer");
        int captures = _canvas.PointerCaptures?.Count ?? 0;
        bool crossAssigned = _canvas.HasCrossCursor;
        var previous = _lastUi;
        bool changed = previous is null || !ReferenceEquals(source, previous.Source)
            || captures != previous.Captures || crossAssigned != previous.CrossAssigned
            || point.Properties.IsLeftButtonPressed != previous.LeftPressed;

        // Keep transitions, but only retain a steady movement heartbeat every 50 ms.
        if (name == "move" && !changed && ElapsedMilliseconds(now - _lastPointerRecord) < 50)
        {
            return;
        }

        string sourceName = source is FrameworkElement element
            ? $"{element.GetType().Name}#{element.Name}"
            : source?.GetType().Name ?? "null";
        var observation = new UiObservation(now, source, sourceName, captures, crossAssigned, point.Properties.IsLeftButtonPressed);
        Volatile.Write(ref _lastUi, observation);
        _lastPointerRecord = now;
        Record("pointer", FormattableString.Invariant($"event={name} source={sourceName} canvasSource={ReferenceEquals(source, _canvas)} crossAssigned={crossAssigned} captures={captures} left={point.Properties.IsLeftButtonPressed} right={point.Properties.IsRightButtonPressed} handled={e.Handled} pointDip=({point.Position.X:F2},{point.Position.Y:F2}) pointerTimestamp={point.Timestamp} threadCursor={CursorName(GetCursor())} captureHwnd=0x{GetCapture():X}"), now);

        if (name == "enter")
        {
            // Input-site child HWNDs can be created after the overlay constructor.
            _probe?.Refresh();
        }
    }

    private void RecordWindow(string description)
    {
        if (description.StartsWith("native-probe-health", StringComparison.Ordinal))
        {
            Record("probe-health", description);
            return;
        }

        lock (_gate)
        {
            _metadata.Add(description);
        }
    }

    private void OnDiagnosticTick(DispatcherQueueTimer sender, object args)
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        RecordInputState("timer");
        long now = Stopwatch.GetTimestamp();
        if (ElapsedMilliseconds(now - _lastHealthCheck) >= 1000)
        {
            _lastHealthCheck = now;
            _probe?.Refresh();
            _probe?.CheckHealth("timer");
        }
    }

    private void AttachInputSource()
    {
        long now = Stopwatch.GetTimestamp();
        if (_inputSource is not null || (_lastInputAttempt != 0 && ElapsedMilliseconds(now - _lastInputAttempt) < 1000))
        {
            return;
        }

        _lastInputAttempt = now;
        try
        {
            // Wait for a routed pointer event before looking up the island. WinUI has
            // already established its input source by then; GetForIsland returns it.
            var island = ContentIsland.GetByVisual(ElementCompositionPreview.GetElementVisual(_canvas));
            if (island is null || island.IsClosed)
            {
                Record("input-source-status", "No island found for the canvas visual; will retry on a later pointer event.");
                return;
            }

            _inputSource = InputPointerSource.GetForIsland(island);
            if (_inputSource is null)
            {
                Record("input-source-status", $"No input source available for island={island.Id}.");
                return;
            }

            _islandId = island.Id;
            _inputSource.PointerMoved += OnInputMoved;
            _inputSource.PointerEntered += OnInputEntered;
            _inputSource.PointerExited += OnInputExited;
            _inputSource.PointerRoutedAway += OnInputRoutedAway;
            _inputSource.PointerRoutedTo += OnInputRoutedTo;
            _inputSource.PointerCaptureLost += OnInputCaptureLost;
            Record("input-source-status", $"attached island={_islandId} appWindowId={island.Environment.AppWindowId.Value} uiTid={GetCurrentThreadId()}");
        }
        catch (Exception ex)
        {
            DetachInputSource();
            Record("input-source-error", ex.ToString());
        }
    }

    private void OnInputMoved(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("move", args);

    private void OnInputEntered(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("enter", args);

    private void OnInputExited(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("exit", args);

    private void OnInputRoutedAway(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("routed-away", args);

    private void OnInputRoutedTo(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("routed-to", args);

    private void OnInputCaptureLost(InputPointerSource sender, PointerEventArgs args) => RecordInputEvent("capture-lost", args);

    private void RecordInputEvent(string name, PointerEventArgs args)
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _inputEvents);
        try
        {
            if (!_dispatcher.HasThreadAccess)
            {
                Record("input-source-error", $"Unexpected callback thread={GetCurrentThreadId()}; UI state was not read.");
                return;
            }

            var point = args.CurrentPoint;
            RecordInputState($"event-{name}", force: name != "move", pointerTimestamp: point.Timestamp);
        }
        catch (Exception ex)
        {
            Record("input-source-error", ex.ToString());
        }
    }

    private void RecordInputState(string reason, bool force = false, ulong pointerTimestamp = 0)
    {
        if (_inputSource is null || Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        try
        {
            // Do not dispose these objects: the input source and its cursor belong to WinUI.
            string shape = _inputSource.Cursor switch
            {
                InputSystemCursor cursor => cursor.CursorShape.ToString(),
                null => "null",
                var cursor => cursor.GetType().Name,
            };
            long now = Stopwatch.GetTimestamp();
            var previous = _lastInput;
            if (!force && previous is not null && previous.Cursor == shape && ElapsedMilliseconds(now - previous.Timestamp) < 50)
            {
                return;
            }

            Volatile.Write(ref _lastInput, new InputObservation(now, shape));
            Record("input-source", $"reason={reason} island={_islandId} sourceCursor={shape} canvasCrossAssigned={_canvas.HasCrossCursor} captures={_canvas.PointerCaptures?.Count ?? 0} pointerTimestamp={pointerTimestamp} uiTid={GetCurrentThreadId()}", now);
        }
        catch (Exception ex)
        {
            Record("input-source-error", ex.ToString());
        }
    }

    private void RecordCursorEvent(CursorWinEventProbe.EventSample sample)
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _cursorEvents);
        RecordInputState("cursor-event");
        var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref info))
        {
            Record("cursor-event-error", $"GetCursorInfo error={Marshal.GetLastPInvokeError()}");
            return;
        }

        long observed = Stopwatch.GetTimestamp();
        bool inside = info.Position.X >= _bounds.X && info.Position.X < (long)_bounds.X + _bounds.Width
            && info.Position.Y >= _bounds.Y && info.Position.Y < (long)_bounds.Y + _bounds.Height;
        if (!inside)
        {
            return;
        }

        var ui = Volatile.Read(ref _lastUi);
        var input = Volatile.Read(ref _lastInput);
        Record("cursor-event", FormattableString.Invariant($"eventTid={sample.EventThread} eventPid={sample.EventProcess} callbackTid={sample.CallbackThread} eventTimeMs={sample.EventTimeMs} deliveryDelayMs={sample.DeliveryDelayMs} eventHwnd=0x{sample.Hwnd:X} childId={sample.ChildId} callbackQpc={sample.CallbackQpc} observedQpc={observed} callbackCursor={CursorName(info.Cursor)} flags=0x{info.Flags:X} screen=({info.Position.X},{info.Position.Y}) lastUi={ui?.SourceName ?? "none"} uiAgeMs={(ui is null ? -1 : ElapsedMilliseconds(observed - ui.Timestamp)):F3} lastInput={input?.Cursor ?? "unavailable"} inputAgeMs={(input is null ? -1 : ElapsedMilliseconds(observed - input.Timestamp)):F3}"), observed);
    }

    private void RecordNativeMessage(CursorMessageProbe.MessageSample sample)
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _nativeMessages);
        bool changed = sample.Before != sample.After;
        if (changed)
        {
            Interlocked.Increment(ref _nativeCursorChanges);
        }

        // Preserve every cursor change and lifecycle message. Bound repetitive messages
        // independently of mouse polling rate; the global sampler supplies another view.
        bool frequent = sample.Message is 0x0020 or 0x0084 or 0x0200 or 0x00A0 or 0x0245;
        var key = (sample.Hwnd, sample.Message);
        if (frequent && !changed && _lastNative.TryGetValue(key, out var previous)
            && previous.Before == sample.Before && previous.After == sample.After && previous.Result == sample.Result
            && (sample.Message != 0x0020 || (previous.WParam == sample.WParam && previous.LParam == sample.LParam))
            && ElapsedMilliseconds(sample.Finished - previous.Finished) < 50)
        {
            return;
        }

        _lastNative[key] = sample;
        string details = sample.Message == 0x0020
            ? $" hitTest={unchecked((short)(sample.LParam.ToInt64() & 0xFFFF))} trigger=0x{(sample.LParam.ToInt64() >> 16) & 0xFFFF:X}"
            : string.Empty;
        Record("native", FormattableString.Invariant($"hwnd=0x{sample.Hwnd:X} msg=0x{sample.Message:X4} wParam=0x{sample.WParam:X} lParam=0x{sample.LParam:X}{details} before={CursorName(sample.Before)} after={CursorName(sample.After)} changed={changed} result=0x{sample.Result:X} startQpc={sample.Started} durationMs={ElapsedMilliseconds(sample.Finished - sample.Started):F3} captureHwnd=0x{GetCapture():X}"), sample.Finished);
    }

    private void SampleCursor()
    {
        long samples = 0;
        long transitions = 0;
        long failures = 0;
        long lastSample = 0;
        long maxGap = 0;
        long lastRecorded = 0;
        CursorInfo previous = default;
        nint previousHwnd = 0;
        bool wasInside = false;
        string stopReason = "60-second-limit";
        try
        {
            while (Volatile.Read(ref _stopRequested) == 0 && ElapsedMilliseconds(Stopwatch.GetTimestamp() - _started) < 60000)
            {
                var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                var ui = Volatile.Read(ref _lastUi);
                var input = Volatile.Read(ref _lastInput);
                long now = Stopwatch.GetTimestamp();
                if (lastSample != 0)
                {
                    maxGap = Math.Max(maxGap, now - lastSample);
                }

                lastSample = now;
                if (GetCursorInfo(ref info))
                {
                    samples++;
                    bool inside = info.Position.X >= _bounds.X && info.Position.X < (long)_bounds.X + _bounds.Width
                        && info.Position.Y >= _bounds.Y && info.Position.Y < (long)_bounds.Y + _bounds.Height;
                    nint hwnd = inside ? WindowFromPoint(info.Position) : 0;
                    bool changed = samples == 1 || info.Cursor != previous.Cursor || info.Flags != previous.Flags;
                    if (samples > 1 && inside && changed)
                    {
                        transitions++;
                    }

                    if ((inside || wasInside) && (changed || inside != wasInside || hwnd != previousHwnd || ElapsedMilliseconds(now - lastRecorded) >= 250))
                    {
                        uint thread = GetWindowThreadProcessId(hwnd, out uint process);
                        string uiState = ui is null ? "none" : FormattableString.Invariant($"{ui.SourceName} crossAssigned={ui.CrossAssigned} captures={ui.Captures} left={ui.LeftPressed} uiAgeMs={ElapsedMilliseconds(now - ui.Timestamp):F3}");
                        string inputState = input is null ? "unavailable" : FormattableString.Invariant($"{input.Cursor} inputAgeMs={ElapsedMilliseconds(now - input.Timestamp):F3}");
                        Record("sample", FormattableString.Invariant($"globalCursor={CursorName(info.Cursor)} flags=0x{info.Flags:X} screen=({info.Position.X},{info.Position.Y}) insideDisplay={inside} underHwnd=0x{hwnd:X} underRoot=0x{GetAncestor(hwnd, 2):X} underTid={thread} underPid={process} foreground=0x{GetForegroundWindow():X} lastUi={uiState} lastInput={inputState}"), now);
                        lastRecorded = now;
                    }

                    previous = info;
                    previousHwnd = hwnd;
                    wasInside = inside;
                }
                else
                {
                    failures++;
                    if (failures == 1)
                    {
                        Record("sample-error", $"GetCursorInfo error={Marshal.GetLastPInvokeError()}", now);
                    }
                }

                // No timer-resolution changes or busy spin; actual cadence is reported below.
                Thread.Sleep(SampleIntervalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopReason = "sampler-error";
            Record("sampler-error", ex.ToString());
        }
        finally
        {
            bool closed = Interlocked.Exchange(ref _stopRequested, 1) != 0;
            if (!closed)
            {
                // Subclass removal must run on the owning UI thread, even when the
                // collection limit is reached on the sampler thread.
                try
                {
                    if (!_dispatcher.TryEnqueue(Dispose))
                    {
                        Record("detach-pending", "Dispatcher shutting down; native registrations will be removed on window destruction.");
                    }
                }
                catch (Exception ex)
                {
                    Record("detach-pending", ex.ToString());
                }
            }

            // Only the sampler waits. The UI never waits for the sampler, and a late
            // dispatcher callback can complete the signal safely after this timeout.
            bool cleanupCompleted = _cleanupCompleted.Task.Wait(TimeSpan.FromSeconds(2));
            if (!cleanupCompleted)
            {
                Record("detach-pending", "UI cleanup did not finish before saving; later cleanup results are not in this trace.");
            }

            Record("summary", FormattableString.Invariant($"reason={(closed ? "closed" : stopReason)} cleanupCompleted={cleanupCompleted} samples={samples} observedChangesInsideDisplay={transitions} failures={failures} maxSampleGapMs={ElapsedMilliseconds(maxGap):F3} nativeMessages={Interlocked.Read(ref _nativeMessages)} nativeCursorChanges={Interlocked.Read(ref _nativeCursorChanges)} inputEvents={Interlocked.Read(ref _inputEvents)} cursorEvents={Interlocked.Read(ref _cursorEvents)}"));
            SaveTrace();
        }
    }

    private string CursorName(nint cursor)
        => $"{(cursor == _cross && cursor != 0 ? "Cross" : cursor == _arrow && cursor != 0 ? "Arrow" : "Other")}(0x{cursor:X})";

    private static double ElapsedMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private void Record(string category, string details, long timestamp = 0)
    {
        long now = timestamp == 0 ? Stopwatch.GetTimestamp() : timestamp;
        string entry = string.Create(CultureInfo.InvariantCulture, $"tMs={ElapsedMilliseconds(now - _started):F3} qpc={now} {category} {details}");
        lock (_gate)
        {
            if (!_recording)
            {
                return;
            }

            if (_entries.Count == MaximumEntries)
            {
                _entries.Dequeue();
                _overwritten++;
            }

            _entries.Enqueue(entry);
        }
    }

    private void SaveTrace()
    {
        List<string> lines;
        lock (_gate)
        {
            _recording = false;
            lines = new List<string>(_metadata);
            lines.Add($"retained={_entries.Count} overwritten={_overwritten}; repetitive unchanged native/pointer events are throttled to 50 ms.");
            lines.AddRange(_entries);
            _entries.Clear();
        }

        try
        {
            File.WriteAllLines(_path, lines);
            Logger.LogInfo($"PowerOCR cursor trace saved: {_path}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Could not save PowerOCR cursor trace.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Record("lifecycle", "overlay diagnostics detached");
        try
        {
            _root.Loaded -= OnLoaded;
            _window.Activated -= OnActivated;
            _window.Closed -= OnClosed;
            foreach (var (routedEvent, handler) in _handlers)
            {
                _root.RemoveHandler(routedEvent, handler);
            }
        }
        catch (Exception ex)
        {
            Record("detach-error", ex.ToString());
        }
        finally
        {
            try
            {
                _diagnosticTimer?.Stop();
                if (_diagnosticTimer is not null)
                {
                    _diagnosticTimer.Tick -= OnDiagnosticTick;
                }
            }
            catch (Exception ex)
            {
                Record("detach-error", ex.ToString());
            }

            _winEventProbe?.Dispose();
            DetachInputSource();
            _canvas.Diagnostics = null;
            _probe?.Dispose();
            Interlocked.Exchange(ref _stopRequested, 1);
            _cleanupCompleted.TrySetResult(true);
        }
    }

    private void DetachInputSource()
    {
        var source = _inputSource;
        _inputSource = null;
        if (source is null)
        {
            return;
        }

        try
        {
            source.PointerMoved -= OnInputMoved;
            source.PointerEntered -= OnInputEntered;
            source.PointerExited -= OnInputExited;
            source.PointerRoutedAway -= OnInputRoutedAway;
            source.PointerRoutedTo -= OnInputRoutedTo;
            source.PointerCaptureLost -= OnInputCaptureLost;
        }
        catch (Exception ex)
        {
            Record("input-source-error", ex.ToString());
        }
    }

    private sealed record UiObservation(long Timestamp, object? Source, string SourceName, int Captures, bool CrossAssigned, bool LeftPressed);

    private sealed record InputObservation(long Timestamp, string Cursor);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int Size;
        public int Flags;
        public nint Cursor;
        public NativePoint Position;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorInfo(ref CursorInfo info);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial nint LoadCursor(nint instance, nint name);

    [LibraryImport("user32.dll")]
    private static partial nint GetCursor();

    [LibraryImport("user32.dll")]
    private static partial nint GetCapture();

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint process);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
