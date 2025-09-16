// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    public static class ToolbarActionExecutor
    {
        public static void Execute(ToolbarAction action)
        {
            if (action == null)
            {
                return;
            }

            switch (action.Type)
            {
                case ToolbarActionType.CommandLine:
                    LaunchProcess(action);
                    break;
                default:
                    break;
            }
        }

        private static void LaunchProcess(ToolbarAction action)
        {
            if (string.IsNullOrWhiteSpace(action.Command))
            {
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = action.Command!,
                    Arguments = action.Arguments ?? string.Empty,
                    WorkingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? Environment.CurrentDirectory : action.WorkingDirectory,
                    UseShellExecute = true,
                    Verb = action.RunAsAdmin ? "runas" : "open",
                };
                Process.Start(psi);
            }
            catch (Win32Exception)
            {
                // User may cancel UAC or file not found; swallow to keep toolbar responsive.
            }
            catch (Exception)
            {
                // TODO: optionally log to PowerToys logger if available.
            }
        }
    }
}
