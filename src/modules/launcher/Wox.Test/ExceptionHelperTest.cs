// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.Helper;

namespace Wox.Test;

[TestClass]
public class ExceptionHelperTest
{
    private const int DwmCompositionDisabledHResult = unchecked((int)0x80263001);

    [TestMethod]
    public void IsRecoverableDwmCompositionException_ReturnsTrue_ForDirectRecoverableComException()
    {
        var exception = CreateComException(DwmCompositionDisabledHResult);

        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(exception));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_ReturnsTrue_ForWrappedRecoverableComException()
    {
        var innerException = CreateComException(DwmCompositionDisabledHResult);
        var exception = new TargetInvocationException(innerException);

        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(exception));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_ReturnsFalse_ForWrappedNonRecoverableException()
    {
        var exception = new TargetInvocationException(new InvalidOperationException("Not a DWM composition failure."));

        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(exception));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_ReturnsTrue_ForDwmCompositionChangedInStackTrace()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(DwmCompositionChangedThrower);

        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(exception));
    }

    private static COMException CreateComException(int hresult)
    {
        var exception = Marshal.GetExceptionForHR(hresult);
        Assert.IsNotNull(exception);
        Assert.IsInstanceOfType<COMException>(exception);
        return (COMException)exception;
    }

    private static void DwmCompositionChangedThrower()
    {
        throw new InvalidOperationException("Stack trace fallback path.");
    }
}
