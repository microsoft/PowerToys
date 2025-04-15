// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public static class ShellCommand
{
    public enum RunAsType
    {
        None,
        Administrator,
        OtherUser,
    }

    public static ProcessStartInfo GetProcessStartInfo(string target, string parentDir, string programArguments, RunAsType runAs = RunAsType.None)
    {
        return new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = parentDir,
            UseShellExecute = true,
            Arguments = programArguments,
            Verb = runAs == RunAsType.Administrator ? "runAs" : runAs == RunAsType.OtherUser ? "runAsUser" : string.Empty,
        };
    }

    public static ProcessStartInfo SetProcessStartInfo(this string fileName, string workingDirectory = "", string arguments = "", string verb = "")
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            Verb = verb,
        };

        return info;
    }
}
