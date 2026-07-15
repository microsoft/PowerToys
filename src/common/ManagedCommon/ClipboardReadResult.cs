// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#pragma warning disable SA1649 // File name should match first type name

namespace ManagedCommon
{
    public readonly struct ClipboardReadResult<T>
        where T : class
    {
        internal ClipboardReadResult(bool succeeded, T? value)
        {
            Succeeded = succeeded;
            Value = value;
        }

        public bool Succeeded { get; }

        public T? Value { get; }

        internal static ClipboardReadResult<T> Success(T value) => new(true, value);

        internal static ClipboardReadResult<T> Failure() => new(false, null);
    }
}

#pragma warning restore SA1649 // File name should match first type name
