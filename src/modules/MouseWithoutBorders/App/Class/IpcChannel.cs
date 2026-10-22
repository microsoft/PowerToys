// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using MouseWithoutBorders.Core;
using StreamJsonRpc;

namespace MouseWithoutBorders
{
    internal sealed class IpcChannel<T>
        where T : new()
    {
        public static T StartIpcServer(string pipeName, CancellationToken cancellationToken)
        {
            SecurityIdentifier securityIdentifier = new SecurityIdentifier(
WellKnownSidType.AuthenticatedUserSid, null);

            PipeSecurity pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                securityIdentifier,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            using (var serverChannel = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity))
                            {
                                await serverChannel.WaitForConnectionAsync();
                                var taskRpc = JsonRpc.Attach(serverChannel, new T());
                                await taskRpc.Completion;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
#if MM_HELPER
                        _ = e;
#else
                        Logger.Log(e);
#endif
                    }
                },
                cancellationToken);

            return default(T);
        }

        public static T StartVerifiedIpcServer(
            string pipeName,
            SecurityIdentifier allowedUser,
            Func<NamedPipeServerStream, string> verifyClientConnection,
            CancellationToken cancellationToken)
        {
            return StartVerifiedIpcServer(pipeName, allowedUser, verifyClientConnection, _ => new T(), null, cancellationToken);
        }

        public static T StartVerifiedIpcServer(
            string pipeName,
            SecurityIdentifier allowedUser,
            Func<NamedPipeServerStream, string> verifyClientConnection,
            Func<NamedPipeServerStream, T> targetFactory,
            CancellationToken cancellationToken)
        {
            return StartVerifiedIpcServer(pipeName, allowedUser, verifyClientConnection, targetFactory, null, cancellationToken);
        }

        internal static T StartVerifiedIpcServer(
            string pipeName,
            SecurityIdentifier allowedUser,
            Func<NamedPipeServerStream, string> verifyClientConnection,
            Action<Exception> serverErrorObserver,
            CancellationToken cancellationToken)
        {
            return StartVerifiedIpcServer(pipeName, allowedUser, verifyClientConnection, _ => new T(), serverErrorObserver, cancellationToken);
        }

        internal static T StartVerifiedIpcServer(
            string pipeName,
            SecurityIdentifier allowedUser,
            Func<NamedPipeServerStream, string> verifyClientConnection,
            Func<NamedPipeServerStream, T> targetFactory,
            Action<Exception> serverErrorObserver,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(verifyClientConnection);
            ArgumentNullException.ThrowIfNull(targetFactory);

            _ = Task.Run(
                async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            using var serverChannel = RestrictedNamedPipeServer.Create(pipeName, allowedUser);
                            await serverChannel.WaitForConnectionAsync(cancellationToken);

                            var rejectionReason = verifyClientConnection(serverChannel);
                            if (!string.IsNullOrEmpty(rejectionReason))
                            {
#if !MM_HELPER
                                Logger.Log($"Rejected Settings IPC client: {rejectionReason}");
#endif
                                if (serverChannel.IsConnected)
                                {
                                    serverChannel.Disconnect();
                                }

                                await Task.Delay(250, cancellationToken);
                                continue;
                            }

                            var taskRpc = JsonRpc.Attach(serverChannel, targetFactory(serverChannel));
                            await taskRpc.Completion;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception e)
                        {
                            serverErrorObserver?.Invoke(e);
#if MM_HELPER
                            _ = e;
#else
                            Logger.Log(e);
#endif
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(250);
                            }
                        }
                    }
                },
                cancellationToken);

            return default(T);
        }
    }
}
