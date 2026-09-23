// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.ProtectedStorage;

/// <summary>Connects only to the current token owner's protected service; never accepts an owner override.</summary>
public sealed class NamedPipeTransport : IProtectedStorageTransport
{
    public async Task<StorageFrame> ExchangeAsync(StorageFrame request, CancellationToken cancellationToken)
    {
        string owner = WindowsIdentity.GetCurrent().User?.Value ?? throw new ProtectedStorageException("Unauthorized");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var pipe = new NamedPipeClientStream(".", $"PowerToysProtectedStorage.Data.{owner}", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        bool sent = false;
        try
        {
            ConnectionIdentity.ValidateOwnerContext();
            ClientIdentity.AllowServiceQuery(owner);
            await ConnectionIdentity.ConnectAsync(pipe, timeout.Token).ConfigureAwait(false);
            using var server = VerifyServer(pipe.SafePipeHandle, owner);
            byte[] bytes = PipeProtocol.Encode(request);
            sent = true;
            await pipe.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            var response = await PipeProtocol.ReadAsync(pipe, timeout.Token).ConfigureAwait(false);
            if (response.Command != request.Command || response.RequestId != request.RequestId)
            {
                throw new InvalidDataException("Mismatched protected storage response.");
            }

            using var verified = VerifyServer(pipe.SafePipeHandle, owner);
            return response;
        }
        catch (Exception exception) when (sent && request.Command is 4 or 6 or 7 or 9 or 10)
        {
            var operationId = request.Metadata.TryGetProperty("operationId", out var operation) &&
                Guid.TryParse(operation.GetString(), out var id) ? id : Guid.Empty;
            throw new ProtectedStorageException("OutcomeUnknown", operationId, exception.HResult, exception);
        }
        catch (ProtectedStorageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException or Win32Exception)
        {
            throw new ProtectedStorageException(sent ? "Timeout" : "NotProvisioned", nativeCode: exception.HResult, innerException: exception);
        }
    }

    private static SafeProcessHandle VerifyServer(SafePipeHandle pipe, string owner)
    {
        if (!GetNamedPipeServerProcessId(pipe, out uint pid))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var process = OpenProcess(0x101000, false, pid);
        if (process.IsInvalid)
        {
            process.Dispose();
            throw new ProtectedStorageException("Unauthorized");
        }

        try
        {
            if (!OpenProcessToken(process, 8, out var token))
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            using (token)
            {
                GetTokenInformation(token, 1, IntPtr.Zero, 0, out int size);
                if (size <= 0 || size > 65536)
                {
                    throw new ProtectedStorageException("Unauthorized");
                }

                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, 1, buffer, size, out _))
                    {
                        throw new ProtectedStorageException("Unauthorized");
                    }

                    var sid = new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
                    var expected = (SecurityIdentifier)new NTAccount($@"NT SERVICE\PowerToysProtectedStorage_{owner}").Translate(typeof(SecurityIdentifier));
                    if (sid != expected)
                    {
                        throw new ProtectedStorageException("Unauthorized");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            var image = new char[32768];
            uint length = (uint)image.Length;
            if (!QueryFullProcessImageName(process, 0, image, ref length))
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            string expectedImage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToysProtectedStorage", "Owners", owner, "Code", "Runtime.exe");
            if (!string.Equals(new string(image, 0, (int)length), expectedImage, StringComparison.OrdinalIgnoreCase) ||
                !GetNamedPipeServerProcessId(pipe, out uint currentPid) || currentPid != pid)
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            ServiceIdentity.VerifyRuntime(process, owner, expectedImage);
            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] name, ref uint size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr information, int length, out int required);
}
