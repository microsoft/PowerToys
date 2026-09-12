// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Contracts;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

internal sealed class FakePowerDisplayCliService : IPowerDisplayCliService
{
    private int _getProfilesCallCount;

    internal Func<CancellationToken, Task<PowerDisplayCliResult<CliProfileListResult>>> GetProfilesHandler { get; set; }
        = _ => Task.FromResult(PowerDisplayCliResult<CliProfileListResult>.Success(new CliProfileListResult()));

    internal Func<int, CancellationToken, Task<PowerDisplayCliResult<CliApplyProfileResult>>> ApplyProfileHandler { get; set; }
        = (_, _) => Task.FromResult(
            PowerDisplayCliResult<CliApplyProfileResult>.Failure(PowerDisplayCliFailureKind.ProcessFailure));

    internal int GetProfilesCallCount => Volatile.Read(ref _getProfilesCallCount);

    internal int? LastAppliedProfileId { get; private set; }

    internal TaskCompletionSource<bool> GetProfilesObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<PowerDisplayCliResult<CliProfileListResult>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _getProfilesCallCount);
        GetProfilesObserved.TrySetResult(true);
        return GetProfilesHandler(cancellationToken);
    }

    public Task<PowerDisplayCliResult<CliApplyProfileResult>> ApplyProfileAsync(
        int profileId,
        CancellationToken cancellationToken)
    {
        LastAppliedProfileId = profileId;
        return ApplyProfileHandler(profileId, cancellationToken);
    }
}
