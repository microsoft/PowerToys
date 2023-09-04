// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace EnvironmentVariables.Models
{
    public class VariablesSet
    {
        public Guid Id { get; }

        public string Name { get; }

        public VariablesSetType Type { get; }

        public List<Variable> Variables { get; }

        public VariablesSet(Guid id, string name, VariablesSetType type, List<Variable> variables)
        {
            Id = id;
            Name = name;
            Type = type;
            Variables = variables;
        }
    }
}
