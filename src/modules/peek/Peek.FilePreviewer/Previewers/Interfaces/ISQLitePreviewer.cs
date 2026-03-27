// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;

using Peek.FilePreviewer.Previewers.SQLitePreviewer.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface ISQLitePreviewer : IPreviewer, IPreviewTarget, IDisposable
    {
        ObservableCollection<SQLiteTableInfo> Tables { get; }

        string? TableCountText { get; }
    }
}
