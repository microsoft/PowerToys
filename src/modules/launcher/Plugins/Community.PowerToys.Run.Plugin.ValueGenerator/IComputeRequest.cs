// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Community.PowerToys.Run.Plugin.ValueGenerator
{
    public interface IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description { get; }

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        bool Compute();

        public string ResultToString();
    }
}
