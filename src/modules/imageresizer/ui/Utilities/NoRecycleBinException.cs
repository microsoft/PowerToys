// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace ImageResizer.Utilities;

/// <summary>
/// The exception that is thrown when a file delete operation cannot be completed because the file
/// is on a drive which does not support a Recycle Bin.
/// </summary>
internal class NoRecycleBinException : IOException
{
    internal NoRecycleBinException(string message)
        : base(message)
    {
    }
}
