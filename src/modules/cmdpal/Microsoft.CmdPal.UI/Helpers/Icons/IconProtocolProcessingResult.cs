// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class IconProtocolProcessingResult : IDisposable
{
    private IconPathConverter.PreparedIcon? _preparedIcon;
    private IRandomAccessStream? _bitmapStream;

    private IconProtocolProcessingResult(
        ResultKind kind,
        IconPathConverter.PreparedIcon? preparedIcon = null,
        IRandomAccessStream? bitmapStream = null,
        string[]? fallbackIconStrings = null)
    {
        Kind = kind;
        _preparedIcon = preparedIcon;
        _bitmapStream = bitmapStream;
        FallbackIconStrings = fallbackIconStrings;
    }

    public ResultKind Kind { get; }

    public IRandomAccessStream? BitmapStream => _bitmapStream;

    public string[]? FallbackIconStrings { get; }

    public static IconProtocolProcessingResult Empty() => new(ResultKind.Empty);

    public static IconProtocolProcessingResult FromPreparedIcon(IconPathConverter.PreparedIcon preparedIcon) =>
        new(ResultKind.PreparedIcon, preparedIcon: preparedIcon);

    public static IconProtocolProcessingResult FromBitmapStream(IRandomAccessStream bitmapStream) =>
        new(ResultKind.BitmapStream, bitmapStream: bitmapStream);

    public static IconProtocolProcessingResult FromFallbackIconStrings(string[] fallbackIconStrings) =>
        new(ResultKind.FallbackIconStrings, fallbackIconStrings: fallbackIconStrings);

    public IconPathConverter.PreparedIcon? TakePreparedIcon() =>
        Interlocked.Exchange(ref _preparedIcon, null);

    public void Dispose()
    {
        Interlocked.Exchange(ref _preparedIcon, null)?.Dispose();
        Interlocked.Exchange(ref _bitmapStream, null)?.Dispose();
    }

    internal enum ResultKind
    {
        Empty,
        PreparedIcon,
        BitmapStream,
        FallbackIconStrings,
    }
}
