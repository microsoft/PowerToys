// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Models
{
    public abstract class SelectedItem
    {
        public abstract bool Matches(string? path);
    }
}
