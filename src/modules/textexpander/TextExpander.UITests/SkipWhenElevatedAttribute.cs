// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Skips a test class while this test process runs above medium integrity, as it does elevated: the engine never types
/// into a window above medium integrity, such as <see cref="TypingTarget"/>, and Settings started from an elevated
/// process turns off Manage snippets. The level is read as the engine's InputTarget reads its own. MSTest checks it
/// before creating the class, so PowerToys never starts. MSTest doesn't inherit this attribute, so every test class
/// carries it, and <see cref="TextExpanderTestBase"/> checks that it does.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class SkipWhenElevatedAttribute : ConditionBaseAttribute
{
    /// <summary>Windows' medium integrity RID (<c>SECURITY_MANDATORY_MEDIUM_RID</c>), the highest the engine drives.</summary>
    private const uint MediumIntegrity = 0x2000;

    /// <summary>Microsoft.Testing.Platform's exit code for a run in which no test ran (<c>ZeroTests</c>).</summary>
    private const string NoTestRanExitCode = "8";

    /// <summary>The platform's variable for exit codes that aren't failures, the same as <c>--ignore-exit-code</c>.</summary>
    private const string IgnoredExitCodesVariable = "TESTINGPLATFORM_EXITCODE_IGNORE";

    private static readonly Lazy<uint?> OwnIntegrity = new(ReadOwnIntegrity);

    public SkipWhenElevatedAttribute()
        : base(ConditionMode.Include)
    {
    }

    /// <summary>Gets a value indicating whether the engine could type into this process. An unknown level counts as elevated, as in the engine.</summary>
    public override bool ShouldRun => EngineCanTypeHere;

    public override string IgnoreMessage =>
        $"This test process runs as administrator (integrity level {(OwnIntegrity.Value is uint level ? $"0x{level:X4}" : "unknown")}). " +
        "Text Expander never types into an elevated window, such as this test's, and Settings run as administrator turns off " +
        "Manage snippets. Run the suite without administrator rights, as doc/devdocs/modules/textexpander.md describes.";

    public override string GroupName => nameof(SkipWhenElevatedAttribute);

    private static bool EngineCanTypeHere => OwnIntegrity.Value is uint level && level <= MediumIntegrity;

    /// <summary>
    /// Elevated, every test is skipped, and the platform fails a run in which no test ran. CI runs this suite elevated for
    /// now, so an elevated process doesn't count that as a failure. Set before the platform starts; other failures still count.
    /// </summary>
    [ModuleInitializer]
    internal static void AcceptNoTestRanWhenElevated()
    {
        if (EngineCanTypeHere)
        {
            return;
        }

        var ignored = Environment.GetEnvironmentVariable(IgnoredExitCodesVariable);
        if (string.IsNullOrEmpty(ignored))
        {
            Environment.SetEnvironmentVariable(IgnoredExitCodesVariable, NoTestRanExitCode);
        }
        else if (!ignored.Split(';').Contains(NoTestRanExitCode))
        {
            Environment.SetEnvironmentVariable(IgnoredExitCodesVariable, $"{ignored};{NoTestRanExitCode}");
        }
    }

    /// <summary>Reads this process's integrity level as the last sub-authority of its mandatory label SID, or null when it can't.</summary>
    private static uint? ReadOwnIntegrity()
    {
        // A pseudo-handle that must not be closed.
        if (!Native.OpenProcessToken(Native.GetCurrentProcess(), Native.TOKEN_QUERY, out var token))
        {
            return null;
        }

        try
        {
            // The label is a variable-length SID, so the size is asked for first; this call fails with ERROR_INSUFFICIENT_BUFFER.
            Native.GetTokenInformation(token, Native.TokenIntegrityLevel, IntPtr.Zero, 0, out var size);
            if (size == 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!Native.GetTokenInformation(token, Native.TokenIntegrityLevel, buffer, size, out _))
                {
                    return null;
                }

                var sid = Marshal.PtrToStructure<Native.TOKEN_MANDATORY_LABEL>(buffer).Label.Sid;
                var countPointer = sid == IntPtr.Zero ? IntPtr.Zero : Native.GetSidSubAuthorityCount(sid);
                var count = countPointer == IntPtr.Zero ? (byte)0 : Marshal.ReadByte(countPointer);
                if (count == 0)
                {
                    return null;
                }

                var levelPointer = Native.GetSidSubAuthority(sid, (uint)(count - 1));
                return levelPointer == IntPtr.Zero ? null : unchecked((uint)Marshal.ReadInt32(levelPointer));
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
