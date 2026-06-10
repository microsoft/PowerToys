// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Managed client for PTWorkspacesSvc.  Mirrors the C++ client so Editor /
/// runner / unit tests can talk to the same service without P/Invoke.
/// </summary>
/// <remarks>
/// The wire format is intentionally tiny — see Protocol.h in the service
/// project for the canonical definition.  Keep this file in sync.
/// </remarks>
public static class WorkspacesSvcClient
{
    private const string PipeName = "PTWorkspacesSvc";

    // Matches WorkspacesSvc::kMaxPayloadBytes.
    private const int MaxPayloadBytes = 8 * 1024 * 1024;

    private const int ConnectTimeoutMs = 3000;

    // Opcodes (mirror of WorkspacesSvc::Opcode).
    private const byte OpPing = 0x00;
    private const byte OpGetSettings = 0x01;
    private const byte OpPutSettings = 0x02;
    private const byte OpGetSchemaVersion = 0x03;
    private const byte OpMigrateFromLegacy = 0x04;

    public enum Result : byte
    {
        Ok = 0,
        ServiceUnavailable,
        AuthRejected,
        ProtocolError,
        ServerError,
        PayloadInvalid,
    }

    public static Result Ping() => RoundTrip(OpPing, ReadOnlySpan<byte>.Empty, out _);

    public static Result GetSettings(out string jsonUtf8)
    {
        var rc = RoundTrip(OpGetSettings, ReadOnlySpan<byte>.Empty, out var resp);
        jsonUtf8 = rc == Result.Ok ? Encoding.UTF8.GetString(resp) : string.Empty;
        return rc;
    }

    public static Result PutSettings(string jsonUtf8)
    {
        var bytes = Encoding.UTF8.GetBytes(jsonUtf8 ?? string.Empty);
        return RoundTrip(OpPutSettings, bytes, out _);
    }

    public static Result MigrateFromLegacy(string legacyJsonUtf8)
    {
        var bytes = Encoding.UTF8.GetBytes(legacyJsonUtf8 ?? string.Empty);
        return RoundTrip(OpMigrateFromLegacy, bytes, out _);
    }

    private static Result RoundTrip(byte opcode, ReadOnlySpan<byte> payload, out byte[] response)
    {
        response = Array.Empty<byte>();
        if (payload.Length > MaxPayloadBytes)
        {
            return Result.PayloadInvalid;
        }

        NamedPipeClientStream pipe;
        try
        {
            // TokenImpersonation is required: NamedPipeClientStream defaults
            // to Anonymous, but the service must impersonate us to read our
            // SID for per-user data partitioning.
            pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
                PipeOptions.None, System.Security.Principal.TokenImpersonationLevel.Impersonation);
            pipe.Connect(ConnectTimeoutMs);
        }
        catch (TimeoutException)
        {
            return Result.ServiceUnavailable;
        }
        catch (IOException)
        {
            return Result.ServiceUnavailable;
        }

        using (pipe)
        {
            try
            {
                Span<byte> header = stackalloc byte[5];
                header[0] = opcode;
                BitConverter.TryWriteBytes(header[1..], (uint)payload.Length);
                pipe.Write(header);
                if (payload.Length > 0)
                {
                    pipe.Write(payload);
                }
                pipe.Flush();

                Span<byte> respHeader = stackalloc byte[5];
                if (!ReadExact(pipe, respHeader))
                {
                    return Result.ProtocolError;
                }
                byte status = respHeader[0];
                uint respLen = BitConverter.ToUInt32(respHeader[1..]);
                if (respLen > MaxPayloadBytes)
                {
                    return Result.ProtocolError;
                }
                if (respLen > 0)
                {
                    response = new byte[respLen];
                    if (!ReadExact(pipe, response))
                    {
                        response = Array.Empty<byte>();
                        return Result.ProtocolError;
                    }
                }
                return MapStatus(status);
            }
            catch (IOException)
            {
                return Result.ProtocolError;
            }
        }
    }

    private static bool ReadExact(Stream s, Span<byte> dest)
    {
        int offset = 0;
        while (offset < dest.Length)
        {
            int got = s.Read(dest[offset..]);
            if (got <= 0)
            {
                return false;
            }
            offset += got;
        }
        return true;
    }

    private static Result MapStatus(byte status) => status switch
    {
        0x00 => Result.Ok,
        0x10 or 0x11 => Result.AuthRejected,
        0x20 or 0xFF => Result.ProtocolError,
        0x21 or 0x22 or 0x23 => Result.PayloadInvalid,
        0x30 or 0x31 => Result.ServerError,
        _ => Result.ServerError,
    };
}
