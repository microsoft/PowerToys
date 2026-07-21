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

    internal sealed class NullKnownGoodVcpStore : IKnownGoodVcpStore
    {
        private static readonly IReadOnlyDictionary<byte, KnownGoodVcpFeature> Empty =
            new Dictionary<byte, KnownGoodVcpFeature>();

        public static NullKnownGoodVcpStore Instance { get; } = new();

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId) => Empty;

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature)
        {
        }
    }
}
