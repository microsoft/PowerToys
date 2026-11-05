// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using PowerToys.ProtectedStorage;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class IdentityAccessTests
{
    [TestMethod]
    public void CallerPreparationGrantsProcessQueryWithoutChangingTokenDacl()
    {
        using var process = new SafeProcessHandle(new IntPtr(-1), false);
        Assert.IsTrue(OpenProcessToken(process, 0x0008 | 0x20000, out var token));
        using (token)
        {
            var original = ReadDescriptor(process.DangerousGetHandle());
            string before = ReadDescriptor(token.DangerousGetHandle()).GetSddlForm(AccessControlSections.Access);
            var observer = new SecurityIdentifier("S-1-5-80-1-2-3-4-5");
            try
            {
                ClientIdentity.AllowServiceQuery(observer);
                Assert.AreEqual(before, ReadDescriptor(token.DangerousGetHandle()).GetSddlForm(AccessControlSections.Access));
                var grant = ReadDescriptor(process.DangerousGetHandle()).DiscretionaryAcl!.OfType<CommonAce>()
                    .Single(ace => ace.SecurityIdentifier == observer);
                Assert.AreEqual(AceQualifier.AccessAllowed, grant.AceQualifier);
                Assert.AreEqual(0x101400, grant.AccessMask);
            }
            finally
            {
                var acl = original.DiscretionaryAcl!;
                var bytes = new byte[acl.BinaryLength];
                acl.GetBinaryForm(bytes, 0);
                Assert.AreEqual(0U, SetSecurityInfo(process.DangerousGetHandle(), 6, 4, IntPtr.Zero, IntPtr.Zero, bytes, IntPtr.Zero));
            }
        }
    }

    [TestMethod]
    public void DesktopConnectionTokenMustMatchOwnerAndSession()
    {
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        using var process = Process.GetCurrentProcess();
        uint session = (uint)process.SessionId;
        var wrongOwner = new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null);
        var ownerFailure = Assert.ThrowsExactly<ProtectedStorageException>(() =>
            ConnectionIdentity.ValidateDesktopOwnerToken(identity.AccessToken, wrongOwner, session));
        Assert.AreEqual("OwnerContextRequired", ownerFailure.ErrorCode);
        Assert.AreEqual(1307, ownerFailure.NativeCode);
        var sessionFailure = Assert.ThrowsExactly<ProtectedStorageException>(() =>
            ConnectionIdentity.ValidateDesktopOwnerToken(identity.AccessToken, identity.User!, session ^ 1U));
        Assert.AreEqual("OwnerContextRequired", sessionFailure.ErrorCode);
        Assert.AreEqual(1307, sessionFailure.NativeCode);
    }

    private static RawSecurityDescriptor ReadDescriptor(IntPtr handle)
    {
        Assert.AreEqual(0U, GetSecurityInfo(handle, 6, 4, IntPtr.Zero, IntPtr.Zero, out _, IntPtr.Zero, out var descriptor));
        try
        {
            uint size = GetSecurityDescriptorLength(descriptor);
            Assert.IsTrue(size > 0 && size <= 65536);
            var bytes = new byte[size];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            return new RawSecurityDescriptor(bytes, 0);
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(IntPtr handle, int type, uint information, IntPtr owner, IntPtr group, out IntPtr dacl, IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll")]
    private static extern uint SetSecurityInfo(IntPtr handle, int type, uint information, IntPtr owner, IntPtr group, byte[] dacl, IntPtr sacl);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
