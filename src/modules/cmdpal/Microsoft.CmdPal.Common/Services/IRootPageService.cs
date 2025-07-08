// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Services;

public interface IRootPageService
{
    Microsoft.CommandPalette.Extensions.IPage GetRootPage();

    Task PreLoadAsync();

    Task PostLoadRootPageAsync();

    void OnPerformTopLevelCommand(object? context);
}
