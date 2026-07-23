// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Windows.ApplicationModel.DataTransfer;

namespace ManagedCommon.UnitTests
{
    internal sealed class TestClipboardBackend : IClipboardBackend
    {
        internal Queue<Exception> GetContentFailures { get; } = new();

        internal Queue<Exception> SetContentFailures { get; } = new();

        internal Queue<Exception> FlushFailures { get; } = new();

        internal DataPackage Content { get; set; } = new();

        internal int GetContentCallCount { get; private set; }

        internal int SetContentCallCount { get; private set; }

        internal int FlushCallCount { get; private set; }

        public DataPackageView GetContent()
        {
            GetContentCallCount++;
            ThrowNext(GetContentFailures);
            return Content.GetView();
        }

        public void SetContent(DataPackage content)
        {
            SetContentCallCount++;
            ThrowNext(SetContentFailures);
            Content = content;
        }

        public void Flush()
        {
            FlushCallCount++;
            ThrowNext(FlushFailures);
        }

        private static void ThrowNext(Queue<Exception> failures)
        {
            if (failures.TryDequeue(out Exception? exception))
            {
                throw exception;
            }
        }
    }
}
