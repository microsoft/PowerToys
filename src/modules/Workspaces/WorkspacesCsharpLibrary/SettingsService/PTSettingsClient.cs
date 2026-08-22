// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Managed client for PTSettingsSvc.  Mirrors the native client and the
/// service's wire protocol (see Protocol.h) so the Editor, runner and unit
/// tests can talk to the service without P/Invoke.  The service treats the
/// payload as opaque bytes; all JSON / schema concerns live in the caller.
/// </summary>
public static class PTSettingsClient
{
    /// <summary>Coarse result surfaced to callers (mirrors the service status bands).</summary>
    public enum Result : byte
    {
        /// <summary>Request succeeded.</summary>
        Ok = 0,

        /// <summary>GetBlob: the blob does not exist yet (service is up).</summary>
        NotFound,

        /// <summary>Caller authentication / namespace check failed.</summary>
        AuthRejected,

        /// <summary>No service to talk to (not installed / not running).</summary>
        Unavailable,

        /// <summary>Framing / unexpected protocol error.</summary>
        Protocol,

        /// <summary>Underlying file IO failed in the service.</summary>
        IoError,
    }

    // Mirror of PTSettingsSvc pipe naming (Approach 4 / §12.8): each user has
    // their own service instance PTSettingsSvc_<SID>, reachable at
    // \\.\pipe\PTSettingsSvc_<SID>.  We derive <SID> from OUR OWN token so we
    // always reach our own user's instance.  NamedPipeClientStream takes the
    // name without the \\.\pipe\ prefix.  Computed lazily and cached.
    private const string PipeNamePrefix = "PTSettingsSvc_";

    private static readonly Lazy<string> _pipeName = new(() =>
        PipeNamePrefix + (WindowsIdentity.GetCurrent().User?.Value ?? string.Empty));

    public static string PipeName => _pipeName.Value;

    // Mirror of PTSettingsSvc::kMaxPayloadBytes (1 MiB).
    private const int MaxPayloadBytes = 1 * 1024 * 1024;

    private const int ConnectTimeoutMs = 3000;

    // Opcodes (mirror of PTSettingsSvc::Opcode).
    private const byte OpPing = 0x00;
    private const byte OpGetBlob = 0x01;
    private const byte OpPutBlob = 0x02;

    /// <summary>Liveness probe.  Authentication still runs server-side.</summary>
    public static Result Ping()
    {
        return RoundTrip(OpPing, ReadOnlySpan<byte>.Empty, out _);
    }

    /// <summary>Reads this caller's namespace blob.  Returns NotFound if none exists yet.</summary>
    public static Result GetBlob(out byte[] blob)
    {
        var rc = RoundTrip(OpGetBlob, ReadOnlySpan<byte>.Empty, out var resp);
        blob = rc == Result.Ok ? resp : Array.Empty<byte>();
        return rc;
    }

    /// <summary>Atomically replaces this caller's namespace blob with the given bytes.</summary>
    public static Result PutBlob(ReadOnlySpan<byte> blob)
    {
        return RoundTrip(OpPutBlob, blob, out _);
    }

    private static Result RoundTrip(byte opcode, ReadOnlySpan<byte> payload, out byte[] response)
    {
        response = Array.Empty<byte>();
        if (payload.Length > MaxPayloadBytes)
        {
            return Result.Protocol;
        }

        NamedPipeClientStream pipe;
        try
        {
            // TokenImpersonation lets the service impersonate us to read our
            // SID (per-user data partitioning) and open our process for the
            // image-path / signature checks.
            pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.None,
                TokenImpersonationLevel.Impersonation);
            pipe.Connect(ConnectTimeoutMs);
        }
        catch (TimeoutException)
        {
            return Result.Unavailable;
        }
        catch (IOException)
        {
            return Result.Unavailable;
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Unavailable;
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
                    return Result.Protocol;
                }

                byte status = respHeader[0];
                uint respLen = BitConverter.ToUInt32(respHeader[1..]);
                if (respLen > MaxPayloadBytes)
                {
                    return Result.Protocol;
                }

                if (respLen > 0)
                {
                    response = new byte[respLen];
                    if (!ReadExact(pipe, response))
                    {
                        response = Array.Empty<byte>();
                        return Result.Protocol;
                    }
                }

                return MapStatus(status);
            }
            catch (IOException)
            {
                return Result.Protocol;
            }
        }
    }

    private static bool ReadExact(Stream stream, Span<byte> dest)
    {
        int offset = 0;
        while (offset < dest.Length)
        {
            int got = stream.Read(dest[offset..]);
            if (got <= 0)
            {
                return false;
            }

            offset += got;
        }

        return true;
    }

    private static Result MapStatus(byte status)
    {
        // Mirror of PTSettingsSvc::Status, collapsed to the coarse Result.
        return status switch
        {
            0x00 => Result.Ok,
            0x20 => Result.NotFound,
            0x10 or 0x11 or 0x12 => Result.AuthRejected,
            0x21 => Result.IoError,
            _ => Result.Protocol,
        };
    }
}
