// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Models;

namespace TopToolbar.Providers
{
    public interface IToolbarGroupProvider
    {
        string Id { get; }

        Task<ButtonGroup> CreateGroupAsync(ActionContext context, CancellationToken cancellationToken);
    }
}
