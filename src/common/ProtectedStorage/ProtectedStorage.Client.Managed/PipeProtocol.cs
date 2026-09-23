// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace PowerToys.ProtectedStorage;

/// <summary>PTPS 1.0: little-endian scalar header, network-order UUID, UTF-8 metadata, opaque bytes.</summary>
public static class PipeProtocol
{
    public const int HeaderLength = 36;
    public const int MaximumMetadata = 64 * 1024;
    public const int MaximumBlob = 16 * 1024 * 1024;
    private const uint Magic = 0x53505450;

    public static byte[] Encode(StorageFrame frame)
    {
        var metadata = Encoding.UTF8.GetBytes(frame.Metadata.GetRawText());
        if (frame.Metadata.ValueKind != JsonValueKind.Object || metadata.Length > MaximumMetadata || frame.Bytes.Length > MaximumBlob || frame.RequestId == Guid.Empty || frame.Command is < 1 or > 10)
        {
            throw new InvalidDataException("Invalid protected storage frame.");
        }

        var result = new byte[HeaderLength + 4 + metadata.Length + frame.Bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), frame.Command);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), (uint)(result.Length - HeaderLength));
        frame.RequestId.TryWriteBytes(result.AsSpan(20, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(HeaderLength), (uint)metadata.Length);
        metadata.CopyTo(result, HeaderLength + 4);
        frame.Bytes.CopyTo(result, HeaderLength + 4 + metadata.Length);
        return result;
    }

    public static async Task<StorageFrame> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        int length = ValidateHeader(header);
        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        return DecodeBody(header, body);
    }

    public static StorageFrame Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException("Truncated protected storage header.");
        }

        int length = ValidateHeader(bytes[..HeaderLength]);
        if (bytes.Length != HeaderLength + length)
        {
            throw new InvalidDataException("Invalid protected storage body length.");
        }

        return DecodeBody(bytes[..HeaderLength], bytes[HeaderLength..]);
    }

    private static int ValidateHeader(ReadOnlySpan<byte> header)
    {
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[4..]) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[6..]) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(header[12..]) != HeaderLength ||
            BinaryPrimitives.ReadUInt32LittleEndian(header[8..]) is < 1 or > 10 ||
            length < 4 || length > 4 + MaximumMetadata + MaximumBlob ||
            new Guid(header[20..], bigEndian: true) == Guid.Empty)
        {
            throw new InvalidDataException("Invalid or incompatible protected storage header.");
        }

        return (int)length;
    }

    private static StorageFrame DecodeBody(ReadOnlySpan<byte> header, ReadOnlySpan<byte> body)
    {
        uint metadataLength = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (metadataLength == 0 || metadataLength > MaximumMetadata || metadataLength > body.Length - 4 || body.Length - 4 - metadataLength > MaximumBlob)
        {
            throw new InvalidDataException("Invalid protected storage metadata length.");
        }

        using var document = JsonDocument.Parse(body.Slice(4, (int)metadataLength).ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        ValidateMetadata(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Invalid protected storage metadata.");
        }

        return new StorageFrame(BinaryPrimitives.ReadUInt32LittleEndian(header[8..]), new Guid(header[20..], bigEndian: true), document.RootElement.Clone(), body[(4 + (int)metadataLength)..].ToArray());
    }

    private static void ValidateMetadata(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.EnumerateObject())
            {
                if (!names.Add(item.Name))
                {
                    throw new InvalidDataException("Duplicate protected storage metadata.");
                }

                ValidateMetadata(item.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ValidateMetadata(item);
            }
        }
        else if (value.ValueKind == JsonValueKind.Number && !value.TryGetUInt64(out _))
        {
            throw new InvalidDataException("Invalid protected storage number.");
        }
    }
}
