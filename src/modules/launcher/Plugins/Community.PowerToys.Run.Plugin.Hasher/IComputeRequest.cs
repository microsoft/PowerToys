// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public interface IComputeRequest
    {
        public byte[] Result { get; set; }

        public string ErrorMessage { get; set; }

        void Compute();

        public string ResultToString();

        public string FormatResult(IFormatProvider provider = null);
    }
}
