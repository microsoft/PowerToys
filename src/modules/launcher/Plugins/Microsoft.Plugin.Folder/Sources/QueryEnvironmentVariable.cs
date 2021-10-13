// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder.Sources
{
    public class QueryEnvironmentVariable : IQueryEnvironmentVariable
    {
        private readonly IDirectory _directory;

        public QueryEnvironmentVariable(IDirectory directory)
        {
            _directory = directory;
        }

        public IEnumerable<IItemResult> Query(string querySearch)
        {
            if (querySearch == null)
            {
                throw new ArgumentNullException(nameof(querySearch));
            }

            var results = GetEnvironmentVariables(querySearch).OrderBy(v => v.Title);

            if (querySearch.Equals('%'))
            {
                return results;
            }
            else
            {
                return results.Where(v => v.Title.StartsWith(querySearch, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public IEnumerable<EnvironmentVariableResult> GetEnvironmentVariables(string querySearch)
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
            {
                var name = "%" + (string)variable.Key + "%";
                var path = (string)variable.Value;

                if (_directory.Exists(path))
                {
                    yield return new EnvironmentVariableResult(querySearch, name, Environment.ExpandEnvironmentVariables(path));
                }
            }
        }
    }
}
