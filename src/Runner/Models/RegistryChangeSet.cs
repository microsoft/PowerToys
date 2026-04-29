// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RunnerV2.Models
{
    internal readonly struct RegistryChangeSet
    {
        public RegistryValueChange[] Changes { get; init; }

        public readonly bool IsApplied => Changes.All(c => c.IsApplied);

        public readonly bool Apply()
        {
            bool allApplied = true;
            foreach (var change in Changes)
            {
                allApplied = (change.Apply() || !change.Required) && allApplied;
            }

            return allApplied;
        }

        public readonly bool ApplyIfNotApplied() => IsApplied || Apply();

        public readonly bool UnApplyIfApplied() => !IsApplied || UnApply();

        public readonly bool UnApply()
        {
            bool allUnapplied = true;
            foreach (var change in Changes)
            {
                allUnapplied = (change.UnApply() || !change.Required) && allUnapplied;
            }

            return allUnapplied;
        }
    }
}
