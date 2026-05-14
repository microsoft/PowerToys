// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.Helper;

namespace Wox.Test;

[TestClass]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming conventions")]
public class ExceptionHelperTest
{
    private const int DWM_E_COMPOSITIONDISABLED = unchecked((int)0x80263001);
    private const int STATUS_MESSAGE_LOST_HR = unchecked((int)0xD0000701);
    private const string PresentationFrameworkSource = "PresentationFramework";

    [TestMethod]
    public void IsRecoverableDwmCompositionException_NullException_ReturnsFalse()
    {
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(null));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_NonCOMException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Test");
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_COMException_CompositionDisabled_ReturnsTrue()
    {
        var ex = new COMException("Desktop composition is disabled", DWM_E_COMPOSITIONDISABLED);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_COMException_OtherHResult_ReturnsFalse()
    {
        var ex = new COMException("Some other COM error", unchecked((int)0x80004005));
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_WrappingCompositionDisabled_ReturnsTrue()
    {
        var inner = new COMException("Desktop composition is disabled", DWM_E_COMPOSITIONDISABLED);
        var ex = new TargetInvocationException("Invocation failed", inner);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_WrappingMessageLostFromPresentationFramework_ReturnsTrue()
    {
        var inner = new COMException("Message lost", STATUS_MESSAGE_LOST_HR)
        {
            Source = PresentationFrameworkSource,
        };
        var ex = new TargetInvocationException("Invocation failed", inner);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_WrappingDwmCompositionChangedStackTrace_ReturnsTrue()
    {
        var inner = CreateDwmCompositionChangedComException();
        var ex = new TargetInvocationException("Invocation failed", inner);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_WrappingUnrelatedCOMException_ReturnsFalse()
    {
        var inner = new COMException("Unrelated", unchecked((int)0x80004005));
        var ex = new TargetInvocationException("Invocation failed", inner);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_WrappingNonCOMException_ReturnsFalse()
    {
        var inner = new InvalidOperationException("Not a COM exception");
        var ex = new TargetInvocationException("Invocation failed", inner);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationException_NullInner_ReturnsFalse()
    {
        var ex = new TargetInvocationException("No inner", null);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    private static COMException CreateDwmCompositionChangedComException()
    {
        try
        {
            ThrowDwmCompositionChangedComException();
            throw new AssertFailedException("Expected COMException to be thrown.");
        }
        catch (COMException ex)
        {
            return ex;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDwmCompositionChangedComException()
    {
        throw new COMException("DWM composition changed", unchecked((int)0x80004005));
    }
}
