// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Compiled by Windows PowerShell 5.1. Keep this file C# 5 compatible.
using System;
using System.Runtime.InteropServices;

namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class ClipboardObservation
    {
        private ClipboardObservation(string digest, int? errorHResult)
        {
            Digest = digest;
            ErrorHResult = errorHResult;
        }

        // Empty when the clipboard is unavailable; check Available before using it.
        public string Digest { get; private set; }

        public int? ErrorHResult { get; private set; }

        public bool Available
        {
            get { return !ErrorHResult.HasValue; }
        }

        internal static ClipboardObservation Read(Func<string> readDigest)
        {
            try
            {
                return new ClipboardObservation(readDigest(), null);
            }
            catch (ExternalException error)
            {
                if (error.ErrorCode != unchecked((int)0x800401D0))
                {
                    throw;
                }

                return new ClipboardObservation(string.Empty, error.ErrorCode);
            }
        }
    }
}
