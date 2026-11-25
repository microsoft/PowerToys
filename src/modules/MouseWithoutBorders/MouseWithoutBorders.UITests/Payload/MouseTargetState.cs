// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1 in the disposable guest. Keep this file C# 5 compatible.
namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class MouseTargetState
    {
        public long Window { get; set; }

        public long RootWindow { get; set; }

        public int ProcessId { get; set; }

        public long CaptureWindow { get; set; }

        public bool LeftButtonDown { get; set; }
    }
}
