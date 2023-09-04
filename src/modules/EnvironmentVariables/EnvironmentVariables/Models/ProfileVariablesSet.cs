// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace EnvironmentVariables.Models
{
    public class ProfileVariablesSet : VariablesSet
    {
        public ProfileVariablesSet(Guid id, string name, List<Variable> variables)
            : base(id, name, VariablesSetType.UserProfile, variables)
        {
        }
    }
}
