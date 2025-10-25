// Copyright (c) Microsoft Corporation\r
// The Microsoft Corporation licenses this file to you under the MIT license.\r
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerLauncher.Helper
{
    internal static class ExceptionHelper
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int DWM_E_COMPOSITIONDISABLED = unchecked((int)0x80263001);
#pragma warning restore SA1310 // Field names should not contain underscore

        /// <summary>
        /// Returns true if the exception is a recoverable DWM composition exception.
        /// </summary>
        internal static bool IsRecoverableDwmCompositionException(Exception exception)
        {
            if (exception is not COMException comException)
            {
                return false;
            }

            if (comException.HResult is DWM_E_COMPOSITIONDISABLED)
            {
                return true;
            }

            var stackTrace = comException.StackTrace;
            // Check for common DWM composition changed patterns in the stack trace
            return !string.IsNullOrEmpty(stackTrace) &&
                   stackTrace.Contains("DwmCompositionChanged");
        }
    }
}