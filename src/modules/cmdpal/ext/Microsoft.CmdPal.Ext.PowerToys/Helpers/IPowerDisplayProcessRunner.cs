// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PowerToysExtension.Helpers;

internal interface IPowerDisplayProcessRunner
{
    Task<PowerDisplayProcessResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
