// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wox.Plugin;

namespace Wox.Plugin
{
    public class GenericHistory
    {
        public IList<GenericSelectedItem> SelectedItems { get; set; } = new List<GenericSelectedItem>();
    }
}
