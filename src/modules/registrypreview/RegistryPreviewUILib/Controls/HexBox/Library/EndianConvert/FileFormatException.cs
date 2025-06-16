// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     2020-... created by Filip Jeremic (fjeremic) as "HexView.Wpf".
//     2024-... republished by @hotkidfamily as "HexBox.WinUI".
//     2025 Included in PowerToys. (Branch master; commit 72dcf64dc858c693a7a16887004c8ddbab61fce7.)
// </history>

namespace RegistryPreviewUILib.HexBox.Library.EndianConvert
{
    using System;

    /// <summary>
    /// The exception that is thrown when an input file or a data stream is malformed.
    /// </summary>
    [Serializable]
    public sealed class FileFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileFormatException"/> class.
        /// </summary>
        public FileFormatException()
        {
            // Void
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFormatException"/> class with a specified error message.
        /// </summary>
        ///
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
        public FileFormatException(string message)
        : base(message)
        {
            // Void
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFormatException"/> class with a specified error message and a reference to the inner
        /// exception that is the cause of this exception.
        /// </summary>
        ///
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
        ///
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or a null reference if no inner exception is specified.
        /// </param>
        public FileFormatException(string message, Exception innerException)
        : base(message, innerException)
        {
            // Void
        }
    }
}
