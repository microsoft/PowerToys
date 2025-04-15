// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace EnvironmentVariablesUILib.Models
{
    public class DefaultVariablesSet : VariablesSet
    {
        public DefaultVariablesSet(Guid id, string name, VariablesSetType type)
            : base(id, name, type)
        {
        }
    }
}
