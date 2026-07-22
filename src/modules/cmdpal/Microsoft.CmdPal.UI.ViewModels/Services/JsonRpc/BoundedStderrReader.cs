// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Reads a child process's standard error stream and forwards it to a sink under strict bounds so a
/// chatty or adversarial extension cannot exhaust host memory or flood the log. Individual lines are
/// truncated beyond a byte cap, the number of lines per time window is rate limited, and the total
/// volume forwarded is capped. The underlying stream is still drained to EOF even after the budget is
/// reached so the child is never blocked writing to a full pipe.
/// </summary>
internal sealed class BoundedStderrReader
{
    internal const int DefaultReadChunkBytes = 4 * 1024;
    internal const int DefaultMaxLineBytes = 8 * 1024;
    internal const long DefaultMaxTotalBytes = 1024 * 1024;
    internal const int DefaultMaxLinesPerWindow = 200;

    private static readonly TimeSpan DefaultRateWindow = TimeSpan.FromSeconds(1);

    private readonly Action<string> _sink;
    private readonly int _readChunkBytes;
    private readonly long _maxTotalBytes;
    private readonly int _maxLinesPerWindow;
    private readonly long _rateWindowMs;

    private readonly byte[] _line;
    private int _lineLength;
    private bool _lineTruncated;

    private long _totalLoggedBytes;
    private bool _budgetNoticeEmitted;
    private long _windowStartMs;
    private int _linesInWindow;
    private long _suppressedInWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedStderrReader"/> class.
    /// </summary>
    /// <param name="sink">Receives each forwarded line. It is expected not to throw.</param>
    /// <param name="maxLineBytes">The maximum number of bytes kept for a single line before truncation.</param>
    /// <param name="maxTotalBytes">The maximum total number of forwarded content bytes before further output is suppressed.</param>
    /// <param name="maxLinesPerWindow">The maximum number of lines forwarded within each rate-limit window.</param>
    /// <param name="rateWindow">The rate-limit window length.</param>
    /// <param name="readChunkBytes">The size of the read buffer.</param>
    public BoundedStderrReader(
        Action<string> sink,
        int? maxLineBytes = null,
        long? maxTotalBytes = null,
        int? maxLinesPerWindow = null,
        TimeSpan? rateWindow = null,
        int? readChunkBytes = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _readChunkBytes = Math.Max(256, readChunkBytes ?? DefaultReadChunkBytes);
        _maxTotalBytes = Math.Max(1, maxTotalBytes ?? DefaultMaxTotalBytes);
        _maxLinesPerWindow = Math.Max(1, maxLinesPerWindow ?? DefaultMaxLinesPerWindow);
        _rateWindowMs = (long)(rateWindow ?? DefaultRateWindow).TotalMilliseconds;
        _line = new byte[Math.Max(64, maxLineBytes ?? DefaultMaxLineBytes)];
        _windowStartMs = Environment.TickCount64;
    }

    /// <summary>
    /// Gets the number of content lines forwarded to the sink.
    /// </summary>
    public long LinesEmitted { get; private set; }

    /// <summary>
    /// Gets the number of lines that were truncated because they exceeded the per-line byte cap.
    /// </summary>
    public long LinesTruncated { get; private set; }

    /// <summary>
    /// Gets the number of lines dropped by the rate limit or the total-volume cap.
    /// </summary>
    public long LinesSuppressed { get; private set; }

    /// <summary>
    /// Gets the total number of content bytes forwarded so far.
    /// </summary>
    public long TotalLoggedBytes => _totalLoggedBytes;

    /// <summary>
    /// Gets a value indicating whether the total-volume cap has been reached.
    /// </summary>
    public bool BudgetExhausted => _budgetNoticeEmitted;

    /// <summary>
    /// Drains <paramref name="stream"/> until EOF or cancellation, forwarding bounded stderr to the sink.
    /// </summary>
    /// <param name="stream">The standard error stream to read.</param>
    /// <param name="cancellationToken">A token used to stop reading.</param>
    /// <returns>A task that completes when the stream ends or reading is cancelled.</returns>
    public async Task PumpAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buffer = new byte[_readChunkBytes];

        while (!cancellationToken.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                Append(buffer[i]);
            }
        }

        // Forward any trailing line that was not newline-terminated before EOF.
        if (_lineLength > 0 || _lineTruncated)
        {
            EmitLine();
            ResetLine();
        }
    }

    private void Append(byte value)
    {
        if (value == (byte)'\n')
        {
            EmitLine();
            ResetLine();
            return;
        }

        if (value == (byte)'\r')
        {
            // Carriage returns are terminators, handled by the following line feed or trailing flush.
            return;
        }

        if (_lineLength < _line.Length)
        {
            _line[_lineLength++] = value;
        }
        else
        {
            // Beyond the per-line cap: discard the extra bytes but remember the line was truncated.
            _lineTruncated = true;
        }
    }

    private void ResetLine()
    {
        _lineLength = 0;
        _lineTruncated = false;
    }

    private void EmitLine()
    {
        if (_lineLength == 0 && !_lineTruncated)
        {
            return;
        }

        if (!HasNonWhitespaceContent() && !_lineTruncated)
        {
            return;
        }

        RollRateWindowIfNeeded();

        if (_linesInWindow >= _maxLinesPerWindow)
        {
            _suppressedInWindow++;
            LinesSuppressed++;
            return;
        }

        if (_totalLoggedBytes >= _maxTotalBytes)
        {
            if (!_budgetNoticeEmitted)
            {
                _budgetNoticeEmitted = true;
                _sink("[stderr] output limit reached; further extension stderr will not be logged.");
            }

            LinesSuppressed++;
            return;
        }

        var text = Encoding.UTF8.GetString(_line, 0, _lineLength);
        if (_lineTruncated)
        {
            text += " ... (truncated)";
            LinesTruncated++;
        }

        _totalLoggedBytes += _lineLength;
        _linesInWindow++;
        LinesEmitted++;
        _sink(text);
    }

    private bool HasNonWhitespaceContent()
    {
        for (var i = 0; i < _lineLength; i++)
        {
            var c = _line[i];
            if (c != (byte)' ' && c != (byte)'\t')
            {
                return true;
            }
        }

        return false;
    }

    private void RollRateWindowIfNeeded()
    {
        var nowMs = Environment.TickCount64;
        if (nowMs - _windowStartMs < _rateWindowMs)
        {
            return;
        }

        if (_suppressedInWindow > 0)
        {
            var suppressed = _suppressedInWindow;
            _suppressedInWindow = 0;
            _sink($"[stderr rate limit] suppressed {suppressed} line(s) in the previous window.");
        }

        _windowStartMs = nowMs;
        _linesInWindow = 0;
    }
}
