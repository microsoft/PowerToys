// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace EnvironmentVariables.Models
{
    public class Variable
    {
        public string Name { get; }

        public string Values { get; }

        public Variable(string name, string values)
        {
            Name = name;
            Values = values;
        }
    }
}
