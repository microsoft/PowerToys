// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.FilePreviewer.Exceptions
{
    public class ImageLoadingException : Exception
    {
        public ImageLoadingException()
        {
        }

        public ImageLoadingException(string message)
            : base(message)
        {
        }

        public ImageLoadingException(string message,  Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
