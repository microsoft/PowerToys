// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Answers whether synthetic input can reach a window. SendInput can't tell: against an elevated window it
/// reports success while UIPI discards the events, so integrity levels are compared before anything is
/// sent. Text Expander never drives above medium integrity, even when its host is elevated.
/// </summary>
internal static class InputTarget
{
    /// <summary>Windows' medium integrity RID (<c>SECURITY_MANDATORY_MEDIUM_RID</c>).</summary>
    internal const uint MediumIntegrity = 0x2000;

    private static uint? _ourIntegrity;
    private static bool _ourIntegrityRead;
    private static uint _trustCeiling = MediumIntegrity;

    /// <summary>
    /// Gets the highest integrity level this engine may drive. It cannot be promoted by
    /// user-controlled settings or snippet paths.
    /// </summary>
    public static uint TrustCeiling
    {
        get => Volatile.Read(ref _trustCeiling);
    }

    /// <summary>Applies a destination ceiling independent of user-controlled file paths.</summary>
    public static bool ApplyIntegrityCeiling()
    {
        Volatile.Write(ref _trustCeiling, MediumIntegrity);
        return OurIntegrityLevel() is uint ours && ours <= MediumIntegrity;
    }

    /// <summary>
    /// True when input we synthesize can reach <paramref name="window"/>. False when the target can't be
    /// inspected (elevated or protected processes): nothing is sent there, and the trigger stays as typed.
    /// </summary>
    public static bool CanDrive(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = Native.GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        if (processId == (uint)Environment.ProcessId)
        {
            return OurIntegrityLevel() is uint ownLevel && ownLevel <= TrustCeiling;
        }

        IntPtr process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint? theirs = GetIntegrityLevel(process);
            uint? ours = OurIntegrityLevel();
            if (theirs is null || ours is null)
            {
                return false;
            }

            // UIPI blocks input travelling up; equal levels are fine. User-controlled configuration must
            // never authorize high-integrity input.
            uint ceiling = Math.Min(ours.Value, TrustCeiling);
            return theirs.Value <= ceiling;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }

    /// <summary>
    /// True when <paramref name="processId"/> is PowerToys Settings or the snippet editor, where Text Expander must not
    /// expand: a trigger typed into the snippet editor would replace the very text being written.
    /// Asked once per foreground change, never per key. False when the process can't be inspected;
    /// <see cref="CanDrive"/> already refuses those.
    /// </summary>
    public static bool IsPowerToysSettings(uint processId)
    {
        IntPtr process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            char[] path = new char[1024];
            uint length = (uint)path.Length;
            return Native.QueryFullProcessImageNameW(process, 0, path, ref length)
                && IsPowerToysSettingsImage(new string(path, 0, (int)length));
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }

    /// <summary>True when <paramref name="imagePath"/> belongs to Settings or the snippet editor.</summary>
    internal static bool IsPowerToysSettingsImage(string? imagePath)
        => !string.IsNullOrEmpty(imagePath)
            && (string.Equals(Path.GetFileName(imagePath), "PowerToys.Settings.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(imagePath), "PowerToys.TextExpander.Editor.exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>Our own integrity level, read once: it can't change while the process runs.</summary>
    private static uint? OurIntegrityLevel()
    {
        if (!_ourIntegrityRead)
        {
            // A pseudo-handle that must not be closed.
            _ourIntegrity = GetIntegrityLevel(Native.GetCurrentProcess());
            _ourIntegrityRead = true;
        }

        return _ourIntegrity;
    }

    /// <summary>
    /// Reads a process's integrity level as the last sub-authority of its mandatory label SID,
    /// or null when it cannot be determined.
    /// </summary>
    private static uint? GetIntegrityLevel(IntPtr process)
    {
        if (!Native.OpenProcessToken(process, Native.TOKEN_QUERY, out IntPtr token))
        {
            return null;
        }

        try
        {
            // Sized first: the label is a variable-length SID, so the length is asked for rather
            // than assumed. This call is expected to fail with ERROR_INSUFFICIENT_BUFFER.
            Native.GetTokenInformation(token, Native.TokenIntegrityLevel, IntPtr.Zero, 0, out uint size);
            if (size == 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!Native.GetTokenInformation(token, Native.TokenIntegrityLevel, buffer, size, out _))
                {
                    return null;
                }

                var label = Marshal.PtrToStructure<Native.TOKEN_MANDATORY_LABEL>(buffer);
                if (label.Label.Sid == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr countPointer = Native.GetSidSubAuthorityCount(label.Label.Sid);
                if (countPointer == IntPtr.Zero)
                {
                    return null;
                }

                byte count = Marshal.ReadByte(countPointer);
                if (count == 0)
                {
                    return null;
                }

                IntPtr levelPointer = Native.GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));
                if (levelPointer == IntPtr.Zero)
                {
                    return null;
                }

                return unchecked((uint)Marshal.ReadInt32(levelPointer));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Native.CloseHandle(token);
        }
    }
}
