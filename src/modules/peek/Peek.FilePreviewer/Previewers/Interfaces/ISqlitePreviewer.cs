// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using Peek.FilePreviewer.Previewers.SqlitePreviewer.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface ISqlitePreviewer : IPreviewer, IPreviewTarget, IDisposable
    {
        ObservableCollection<SqliteTableInfo> Tables { get; }

        string? TableCountText { get; }

        Task LoadTableDataAsync(SqliteTableInfo tableInfo, CancellationToken cancellationToken);
    }
}
