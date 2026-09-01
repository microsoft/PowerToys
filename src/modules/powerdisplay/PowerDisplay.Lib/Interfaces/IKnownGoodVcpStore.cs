// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Interfaces
{
    public interface IKnownGoodVcpStore
    {
        IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId);

        void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature);
    }
}
