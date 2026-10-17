// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Answers whether synthetic input can reach a window at all.
///
/// SendInput cannot tell us. Microsoft is explicit that "this function fails when it is blocked
/// by UIPI. Note that neither GetLastError nor the return value will indicate the failure was
/// caused by UIPI blocking" — against an elevated window SendInput returns the full event count
/// and the events are discarded further down. Treating that as success is how an expansion ends
/// up having eaten the user's trigger, churned their clipboard, and inserted nothing.
///
/// So the question is asked before anything is sent, by comparing integrity levels: a process
/// may drive windows at its own level or below, and nothing above it.
/// </summary>
internal static class InputTarget
{
    /// <summary>Windows' medium integrity RID (<c>SECURITY_MANDATORY_MEDIUM_RID</c>).</summary>
    internal const uint MediumIntegrity = 0x2000;

    private static uint? _ourIntegrity;
    private static bool _ourIntegrityRead;
    private static uint _trustCeiling = uint.MaxValue;

    /// <summary>
    /// Gets or sets the highest integrity level this engine is willing to type into.
    ///
    /// <para>
    /// Normally irrelevant: an unelevated engine is already capped by UIPI at its own level.
    /// It matters when the engine is <em>elevated</em> and its snippet file is somewhere a
    /// lower-integrity process can write. The engine cannot tell a snippet the user wrote from
    /// one an attacker appended — both arrive as the same line in the same file — so an elevated
    /// engine reading a medium-writable file is a way to get arbitrary text typed into an
    /// elevated window. That is a privilege escalation, and the config file is the vector.
    /// </para>
    ///
    /// <para>
    /// So the rule is: <b>drive no higher than whoever can edit your configuration.</b> Set to
    /// <see cref="MediumIntegrity"/> when the snippet file is user-writable, which costs an
    /// elevated user expansion inside elevated windows and costs everyone else nothing.
    /// </para>
    /// </summary>
    public static uint TrustCeiling
    {
        get => Volatile.Read(ref _trustCeiling);
        set => Volatile.Write(ref _trustCeiling, value);
    }

    /// <summary>
    /// Applies the ceiling implied by where the snippet file lives.
    ///
    /// <para>
    /// Approximated by profile location rather than by reading the file's DACL. A DACL read is
    /// the precise answer, but it is also easy to get subtly wrong, and being wrong in the
    /// permissive direction here reopens the escalation. Anything inside the user's profile is
    /// assumed writable at medium integrity, which is true for every default location the engine
    /// resolves to.
    /// </para>
    /// </summary>
    public static void ApplyConfigTrust(string snippetFilePath)
        => TrustCeiling = CeilingFor(IsElevated(), snippetFilePath);

    /// <summary>
    /// The ceiling implied by an elevation state and a config location. Pure so the rule can be
    /// tested without an elevated process.
    /// </summary>
    internal static uint CeilingFor(bool elevated, string snippetFilePath)
    {
        // Unelevated, UIPI already caps us at our own level; narrowing further would only break
        // ordinary same-level expansion.
        if (!elevated)
        {
            return uint.MaxValue;
        }

        return IsUserWritableLocation(snippetFilePath) ? MediumIntegrity : uint.MaxValue;
    }

    private static bool IsElevated()
        => OurIntegrityLevel() is uint ours && ours > MediumIntegrity;

    internal static bool IsUserWritableLocation(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return !string.IsNullOrEmpty(profile)
                && full.StartsWith(profile.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            // Cannot tell, so assume the worse of the two readings.
            return true;
        }
    }

    /// <summary>
    /// True when input we synthesise can reach <paramref name="window"/>.
    ///
    /// Answers false when the target cannot be inspected at all. An ordinary desktop application
    /// at our own integrity level is always inspectable, so the processes that refuse are the
    /// elevated and protected ones — the very ones that would have discarded our input anyway.
    /// Being wrong in this direction costs a paste and falls back to typing; being wrong in the
    /// other direction costs the user their clipboard and their trigger.
    /// </summary>
    public static bool CanDrive(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        uint processId = 0;
        _ = Native.GetWindowThreadProcessId(window, out processId);
        if (processId == 0)
        {
            return false;
        }

        if (processId == (uint)Environment.ProcessId)
        {
            return true;
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

            // UIPI blocks input travelling up. Equal levels are fine, which is the ordinary
            // case: two medium-integrity desktop applications.
            //
            // Capped by TrustCeiling so an elevated engine whose snippet file anyone can edit
            // still refuses to type into elevated windows. Without that cap, appending a line to
            // a file in the user's profile is enough to get text typed into an admin console.
            uint ceiling = Math.Min(ours.Value, TrustCeiling);
            return theirs.Value <= ceiling;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }

    /// <summary>
    /// Our own integrity level, read once. It cannot change while the process is running, and
    /// this is on the path of every expansion.
    /// </summary>
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
