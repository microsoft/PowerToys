// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Models
{
    public interface IPreviewSettings
    {
        public bool SourceCodeWrapText { get; }

        public bool SourceCodeTryFormat { get; }

        public int SourceCodeFontSize { get; }

        public bool SourceCodeStickyScroll { get; }
    }
}
