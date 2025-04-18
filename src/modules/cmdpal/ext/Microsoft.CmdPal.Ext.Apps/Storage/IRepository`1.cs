// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

public interface IRepository<T>
{
    void Add(T insertedItem);

    void Remove(T removedItem);

    bool Contains(T item);

    void SetList(IList<T> list);

    bool Any();
}
