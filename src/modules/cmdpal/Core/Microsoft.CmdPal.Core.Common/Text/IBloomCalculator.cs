// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Text;

public interface IBloomCalculator
{
    ulong ComputeBloomFilter(string input);

    bool MightContain(ulong candidateBloom, ulong queryBloom);
}
