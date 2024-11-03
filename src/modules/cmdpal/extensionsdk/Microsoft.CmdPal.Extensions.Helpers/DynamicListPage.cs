// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class DynamicListPage : ListPage, IDynamicListPage
{
    public virtual IListItem[] GetItems(string query) => throw new NotImplementedException();
}
