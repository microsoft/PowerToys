// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading;

using Windows.ApplicationModel.DataTransfer;

namespace ManagedCommon
{
    internal sealed partial class ClipboardWritePackage : IDisposable
    {
        private readonly ResourceLease? _resourceLease;
        private bool _transferred;

        internal ClipboardWritePackage(DataPackage content, IDisposable? ownedResource = null)
        {
            ArgumentNullException.ThrowIfNull(content);

            Content = content;
            if (ownedResource is not null)
            {
                var resourceLease = new ResourceLease(ownedResource);
                _resourceLease = resourceLease;
                Content.Destroyed += (_, _) => resourceLease.Dispose();
            }
        }

        internal DataPackage Content { get; }

        internal void TransferOwnership()
        {
            _transferred = true;
        }

        public void Dispose()
        {
            if (!_transferred)
            {
                _resourceLease?.Dispose();
            }
        }

        private sealed partial class ResourceLease : IDisposable
        {
            private IDisposable? _resource;

            internal ResourceLease(IDisposable resource)
            {
                _resource = resource;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _resource, null)?.Dispose();
            }
        }
    }
}
