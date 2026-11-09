// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace MouseWithoutBorders;

internal static class ClipboardHelperIpc
{
    private const string HelperExecutableName = "PowerToys.MouseWithoutBordersHelper.exe";
    private const int ConnectionTimeoutMilliseconds = 10_000;

    internal static string GetPipeName(int sessionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sessionId);

        return $"PowerToys.MouseWithoutBorders.v2.ClipboardHelper.Session.{sessionId}";
    }

    internal static string VerifyClient(NamedPipeServerStream stream, SecurityIdentifier intendedUser, int sessionId)
    {
        return NamedPipePeerVerification.TryVerifyClient(
            stream,
            HelperExecutableName,
            intendedUser.Value,
            sessionId,
            out var rejectionReason)
            ? string.Empty
            : rejectionReason;
    }

    internal static NamedPipeClientStream Connect(string pipeName)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The clipboard helper has no user SID.");
        using var process = Process.GetCurrentProcess();
        var stream = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);
        try
        {
            stream.Connect(ConnectionTimeoutMilliseconds);
            if (!NamedPipePeerVerification.TryVerifyServer(
                stream,
                MouseWithoutBordersIpc.MouseWithoutBordersExecutableFileName,
                user.Value,
                process.SessionId,
                allowLocalSystem: true,
                out var rejectionReason))
            {
                throw new UnauthorizedAccessException($"Clipboard helper connection rejected: {rejectionReason}");
            }

            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
