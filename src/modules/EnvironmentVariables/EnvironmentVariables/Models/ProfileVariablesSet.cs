// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace EnvironmentVariables.Models
{
    public class ProfileVariablesSet : VariablesSet
    {
        public bool IsEnabled { get; set; }

        public ProfileVariablesSet(Guid id, string name)
            : base(id, name, VariablesSetType.Profile)
        {
        }
    }
}
