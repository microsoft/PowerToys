// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Peek.Common.Models;

namespace Peek.Common.Extensions
{
    public sealed class DisposablePropertyStore : IDisposable
    {
        private readonly IPropertyStore _propertyStore;

        public DisposablePropertyStore(IPropertyStore propertyStore)
        {
            _propertyStore = propertyStore;
        }

        public void GetValue(ref PropertyKey key, out PropVariant pv)
        {
            _propertyStore!.GetValue(ref key, out pv);
        }

        public void Dispose()
        {
           Marshal.ReleaseComObject(_propertyStore);
        }
    }
}
