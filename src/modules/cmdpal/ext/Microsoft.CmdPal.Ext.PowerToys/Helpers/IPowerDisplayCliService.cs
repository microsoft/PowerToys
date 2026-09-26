// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Contracts;

namespace PowerToysExtension.Helpers;

internal interface IPowerDisplayCliService
{
    Task<PowerDisplayCliResult<CliProfileListResult>> GetProfilesAsync(CancellationToken cancellationToken);

    Task<PowerDisplayCliResult<CliApplyProfileResult>> ApplyProfileAsync(int profileId, CancellationToken cancellationToken);
}
