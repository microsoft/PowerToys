// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1 in the disposable guest. Keep this file C# 5 compatible.
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class ProcessIdentity
    {
        // Never add CommandLine: the Win10 Sandbox client embeds an AccountPassword there.
        public ProcessIdentity()
        {
            Path = string.Empty;
        }

        public int Id { get; set; }

        public int ParentId { get; set; }

        public int SessionId { get; set; }

        public string Path { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public static ProcessIdentity Capture(int id)
        {
            using (Process process = Process.GetProcessById(id))
            {
                // Tag the failing stage: ERROR_GEN_FAILURE ("A device attached to the system is
                // not functioning") has been observed here without indicating whether the
                // toolhelp32 snapshot walk (ParentId), the process token/session query
                // (SessionId), or QueryFullProcessImageName (ImagePath) is the one that failed.
                int parentId;
                try
                {
                    parentId = NativeSupport.ParentId(id);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("ParentId: " + error.Message, error);
                }

                int sessionId;
                try
                {
                    sessionId = process.SessionId;
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("SessionId: " + error.Message, error);
                }

                string path;
                try
                {
                    path = NativeSupport.ImagePath(id);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("ImagePath: " + error.Message, error);
                }

                DateTime startTimeUtc;
                try
                {
                    startTimeUtc = process.StartTime.ToUniversalTime();
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("StartTime: " + error.Message, error);
                }

                return new ProcessIdentity
                {
                    Id = id,
                    ParentId = parentId,
                    SessionId = sessionId,
                    Path = path,
                    StartTimeUtc = startTimeUtc,
                };
            }
        }

        public bool IsCurrent()
        {
            try
            {
                ProcessIdentity actual = Capture(Id);
                return actual.StartTimeUtc == StartTimeUtc.ToUniversalTime() && actual.SessionId == SessionId &&
                    actual.ParentId == ParentId && string.Equals(actual.Path, Path, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception)
            {
                try
                {
                    using (Process current = Process.GetProcessById(Id))
                    {
                        if (current.HasExited)
                        {
                            return false;
                        }
                    }
                }
                catch (ArgumentException)
                {
                    return false;
                }

                throw;
            }
        }

        public void Stop()
        {
            if (!IsCurrent())
            {
                return;
            }

            Process process;
            try
            {
                process = Process.GetProcessById(Id);
            }
            catch (ArgumentException)
            {
                return;
            }

            using (process)
            {
                try
                {
                    // Revalidate after opening the PID; never kill by executable name.
                    if (process.StartTime.ToUniversalTime() != StartTimeUtc.ToUniversalTime())
                    {
                        throw new InvalidOperationException("PID was reused during cleanup.");
                    }

                    process.Kill();
                    if (!process.WaitForExit(15000))
                    {
                        throw new TimeoutException("Owned process did not exit: " + Id);
                    }
                }
                catch (InvalidOperationException)
                {
                    if (!process.HasExited)
                    {
                        throw;
                    }
                }
                catch (Win32Exception)
                {
                    if (!process.HasExited)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
