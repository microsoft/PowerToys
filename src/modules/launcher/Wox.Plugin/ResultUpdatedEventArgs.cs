// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Wox.Plugin
{
    public class ResultUpdatedEventArgs : EventArgs
    {
        public List<Result> Results { get; private set; }

        public ResultUpdatedEventArgs(List<Result> results = null)
        {
            Results = results;
        }

        public Query Query { get; set; }
    }
}
