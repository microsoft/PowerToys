// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// <summary>
//     TCP Server implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Exceptions;

[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.TcpServer.#Close()", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal class TcpServer
    {
        private readonly TcpListener server;

        internal string Name { get; private set; }

        internal TcpServer(int port, ParameterizedThreadStart job)
        {
            Common.Log($"TCP listening on port: {port}");
            Name = port.ToString(CultureInfo.CurrentCulture);
            server = TcpListener.Create(port);
            StartServer(job);
        }

        private void StartServer(ParameterizedThreadStart job)
        {
            int tryCount = 6;

            do
            {
                try
                {
                    server.Start();
                    break;
                }
                catch (SocketException e)
                {
                    // DHCP error, etc.
                    if (server.LocalEndpoint.ToString().StartsWith("169.254", StringComparison.InvariantCulture) || server.LocalEndpoint.ToString().StartsWith("0.0", StringComparison.InvariantCulture))
                    {
                        throw new ExpectedSocketException($"Error: The machine has limited connectivity on [{server.LocalEndpoint}]!");
                    }

                    if (e.ErrorCode == 10048 /*WSAEADDRINUSE*/)
                    {
                        if (--tryCount >= 0)
                        {
                            Thread.Sleep(500);
                            continue;
                        }

                        if (!Common.IsMyDesktopActive())
                        {
                            // We can just throw the SocketException but to avoid a redundant log entry:
                            throw new ExpectedSocketException($"{nameof(StartServer)}: The desktop is no longer active.");
                        }
                        else
                        {
                            LogError($"WSAEADDRINUSE: {server.LocalEndpoint}: {e.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        Common.TelemetryLogTrace($"Error listening on: {server.LocalEndpoint}: {e.ErrorCode}/{e.Message}", SeverityLevel.Error);
                        throw;
                    }
                }
            }
            while (true);

            Thread t = new(job, Name = "Tcp Server: " + job.Method.Name + " " + server.LocalEndpoint.ToString());
            t.SetApartmentState(ApartmentState.STA);
            t.Start(server);
        }

        internal void Close()
        {
            try
            {
                server?.Stop();
            }
            catch (Exception e)
            {
                Common.Log(e);
            }
        }

        private static bool logged;
        internal static readonly string[] Separator = new[] { " " };

        private void LogError(string log)
        {
            if (!logged)
            {
                logged = true;

                _ = Task.Factory.StartNew(
                    () =>
                {
                    try
                    {
                        using Process proc = new();
                        ProcessStartInfo startInfo = new()
                        {
                            FileName = Environment.ExpandEnvironmentVariables(@"%windir%\System32\netstat.exe"),
                            Arguments = "-nao",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                        };

                        proc.StartInfo = startInfo;
                        _ = proc.Start();

                        string status = proc.StandardOutput.ReadToEnd() + Environment.NewLine;

                        if (proc.ExitCode == 0)
                        {
                            System.Collections.Generic.IEnumerable<string> portLog = status.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(line => line.Contains("LISTENING") && (line.Contains(":15100 ") || line.Contains(":15101 ")));

                            foreach (string portLogLine in portLog)
                            {
                                int pid = 0;
                                Process process = null;

                                try
                                {
                                    // Assuming the format of netstat's output is fixed.
                                    pid = int.Parse(portLogLine.Split(Separator, StringSplitOptions.RemoveEmptyEntries).Last(), CultureInfo.CurrentCulture);
                                    process = Process.GetProcessById(pid);
                                }
                                catch (Exception)
                                {
                                    /* TODO: There was some telemetry here. Log instead? */
                                }

                                /* TODO: There was some telemetry here. Log instead? */
                            }
                        }
                        else
                        {
                            /* TODO: There was some telemetry here. Log instead? */
                        }
                    }
                    catch (Exception)
                    {
                        /* TODO: There was some telemetry here. Log instead? */
                    }
                },
                    System.Threading.CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default);
            }
        }
    }
}
