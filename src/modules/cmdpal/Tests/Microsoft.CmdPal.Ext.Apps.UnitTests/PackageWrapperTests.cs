// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class PackageWrapperTests
{
    private const int ErrorAppDataNotFoundHResult = unchecked((int)0x80071130);

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetIsFramework_ReturnsPackageFlag(bool isFramework)
    {
        Assert.AreEqual(isFramework, PackageWrapper.GetIsFramework(() => isFramework));
    }

    [TestMethod]
    public void GetIsFramework_FastCacheDataUnavailable_DoesNotSuppressRefresh()
    {
        Assert.IsFalse(PackageWrapper.GetIsFramework(static () =>
        {
            Marshal.ThrowExceptionForHR(ErrorAppDataNotFoundHResult);
            return false;
        }));
    }

    [TestMethod]
    public void GetIsFramework_UnexpectedComFailure_DoesNotSuppressRefresh()
    {
        const int unexpectedHResult = unchecked((int)0x80004005);

        Assert.IsFalse(PackageWrapper.GetIsFramework(static () =>
        {
            Marshal.ThrowExceptionForHR(unexpectedHResult);
            return false;
        }));
    }

    [TestMethod]
    public void GetIsFramework_MissingPackage_DoesNotSuppressRefresh()
    {
        Package package = null;

        Assert.IsFalse(PackageWrapper.GetIsFramework(() => package.IsFramework));
    }

    [TestMethod]
    public void GetIsNonRemovable_SystemPackage_ReturnsTrue()
    {
        Assert.IsTrue(PackageWrapper.GetIsNonRemovable(static () => PackageSignatureKind.System));
    }

    [TestMethod]
    public void GetIsNonRemovable_StorePackage_ReturnsFalse()
    {
        Assert.IsFalse(PackageWrapper.GetIsNonRemovable(static () => PackageSignatureKind.Store));
    }

    [TestMethod]
    public void GetIsNonRemovable_FastCacheDataUnavailable_ReturnsSafeDefault()
    {
        Assert.IsTrue(PackageWrapper.GetIsNonRemovable(
            static () => ThrowExceptionForHResult(ErrorAppDataNotFoundHResult)));
    }

    [TestMethod]
    public void GetIsNonRemovable_UnexpectedComFailure_PropagatesException()
    {
        const int unexpectedHResult = unchecked((int)0x80004005);

        var exception = Assert.ThrowsException<COMException>(() =>
            PackageWrapper.GetIsNonRemovable(
                static () => ThrowExceptionForHResult(unexpectedHResult)));

        Assert.AreEqual(unexpectedHResult, exception.HResult);
    }

    private static PackageSignatureKind ThrowExceptionForHResult(int hresult)
    {
        Marshal.ThrowExceptionForHR(hresult);
        return PackageSignatureKind.None;
    }
}
