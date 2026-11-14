// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1 in the disposable guest. Keep this file C# 5 compatible.
namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class WindowInfo
    {
        public WindowInfo()
        {
            Title = string.Empty;
        }

        public long Handle { get; set; }

        public string Title { get; set; }

        public bool Visible { get; set; }

        public bool HasOwner { get; set; }
    }
}
