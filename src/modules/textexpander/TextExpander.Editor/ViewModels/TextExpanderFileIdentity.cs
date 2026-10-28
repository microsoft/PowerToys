// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    internal sealed class TextExpanderFileIdentity
    {
        public static TextExpanderFileIdentity Missing { get; } = new(exists: false, Array.Empty<byte>());

        public TextExpanderFileIdentity(bool exists, byte[] bytes)
        {
            Exists = exists;
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public bool Exists { get; }

        public byte[] Bytes { get; }

        public bool SameAs(TextExpanderFileIdentity other)
            => other is not null
                && Exists == other.Exists
                && (!Exists || Bytes.AsSpan().SequenceEqual(other.Bytes));
    }
}
