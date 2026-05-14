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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "Test code intentionally constructs COMException instances to verify exception-handling logic")]
public class ExceptionHelperTest
{
    private const int DwmCompositionDisabledHResult = unchecked((int)0x80263001);
    private const int StatusMessageLostHResult = unchecked((int)0xD0000701);
    private const int UnrelatedHResult = unchecked((int)0x80004005); // E_FAIL

    /// <summary>
    /// A direct <see cref="COMException"/> with HRESULT 0x80263001 (DWM_E_COMPOSITIONDISABLED)
    /// must be identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_DirectCOMException_ReturnsTrue()
    {
        var ex = new COMException("Desktop composition is disabled", DwmCompositionDisabledHResult);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="TargetInvocationException"/> wrapping the DWM <see cref="COMException"/> must
    /// be identified as recoverable. WPF raises theme-change events via reflection, so the
    /// 0x80263001 <see cref="COMException"/> surfaces wrapped in a
    /// <see cref="TargetInvocationException"/>.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationWrappingDwmCOMException_ReturnsTrue()
    {
        var inner = new COMException("Desktop composition is disabled", DwmCompositionDisabledHResult);
        var ex = new TargetInvocationException(inner);
        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="TargetInvocationException"/> wrapping an unrelated exception must NOT be
    /// identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationWrappingUnrelatedException_ReturnsFalse()
    {
        var inner = new COMException("Some other COM error", UnrelatedHResult);
        var ex = new TargetInvocationException(inner);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="TargetInvocationException"/> with a null inner exception must NOT be
    /// identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_TargetInvocationWithNullInner_ReturnsFalse()
    {
        var ex = new TargetInvocationException(null);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="COMException"/> with HRESULT 0xD0000701 (STATUS_MESSAGE_LOST) and Source
    /// "PresentationFramework" must be identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_StatusMessageLostFromPresentationFramework_ReturnsTrue()
    {
        var ex = new COMException("Status message lost", StatusMessageLostHResult)
        {
            Source = "PresentationFramework",
        };

        Assert.IsTrue(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="COMException"/> with HRESULT 0xD0000701 (STATUS_MESSAGE_LOST) but a
    /// non-PresentationFramework source must NOT be identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_StatusMessageLostFromOtherSource_ReturnsFalse()
    {
        var ex = new COMException("Status message lost", StatusMessageLostHResult)
        {
            Source = "SomeOtherAssembly",
        };

        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A <see cref="COMException"/> with an unrelated HRESULT must NOT be identified as
    /// recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_UnrelatedCOMException_ReturnsFalse()
    {
        var ex = new COMException("Some other COM error", UnrelatedHResult);
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// A non-COM exception must NOT be identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_NonCOMException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Not a COM exception");
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(ex));
    }

    /// <summary>
    /// Null input must NOT be identified as recoverable.
    /// </summary>
    [TestMethod]
    public void IsRecoverableDwmCompositionException_NullException_ReturnsFalse()
    {
        Assert.IsFalse(ExceptionHelper.IsRecoverableDwmCompositionException(null));
    }
}
