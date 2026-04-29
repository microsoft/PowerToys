// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerLauncher.Helper
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming conventions")]
    internal static class ExceptionHelper
    {
        private const string PresentationFrameworkExceptionSource = "PresentationFramework";

        private const int DWM_E_COMPOSITIONDISABLED = unchecked((int)0x80263001);

        // HRESULT for NT STATUS STATUS_MESSAGE_LOST (0xC0000701 | 0x10000000 == 0xD0000701)
        private const int STATUS_MESSAGE_LOST_HR = unchecked((int)0xD0000701);

        /// <summary>
        /// Returns true if the exception is a recoverable DWM composition exception.
        /// Also checks inner exceptions to handle cases where a DWM error is wrapped by another exception type.
        /// </summary>
        internal static bool IsRecoverableDwmCompositionException(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            // Walk the exception chain (up to a reasonable depth) to detect DWM composition errors at any level
            const int maxDepth = 10;
            var current = exception;
            for (var depth = 0; current != null && depth < maxDepth; depth++, current = current.InnerException)
            {
                if (current is COMException comException)
                {
                    if (comException.HResult is DWM_E_COMPOSITIONDISABLED)
                    {
                        return true;
                    }

                    if (comException.HResult is STATUS_MESSAGE_LOST_HR && comException.Source == PresentationFrameworkExceptionSource)
                    {
                        return true;
                    }
                }
            }

            // Check for common DWM composition changed patterns in the stack trace of the outermost exception
            var stackTrace = exception.StackTrace;
            return !string.IsNullOrEmpty(stackTrace) &&
                   stackTrace.Contains("DwmCompositionChanged");
        }
    }
}
