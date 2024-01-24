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
        private readonly IEnvironmentHelper _environmentHelper;

        public QueryEnvironmentVariable(IDirectory directory, IEnvironmentHelper environmentHelper)
        {
            _directory = directory;
            _environmentHelper = environmentHelper;
        }

        public IEnumerable<IItemResult> Query(string querySearch)
        {
            ArgumentNullException.ThrowIfNull(querySearch);

            return GetEnvironmentVariables(querySearch)
                .OrderBy(v => v.Title)
                .Where(v => v.Title.StartsWith(querySearch, StringComparison.InvariantCultureIgnoreCase));
        }

        public IEnumerable<EnvironmentVariableResult> GetEnvironmentVariables(string querySearch)
        {
            foreach (DictionaryEntry variable in _environmentHelper.GetEnvironmentVariables())
            {
                if (variable.Value == null)
                {
                    continue;
                }

                var name = "%" + (string)variable.Key + "%";
                var path = (string)variable.Value;

                if (_directory.Exists(path))
                {
                    yield return new EnvironmentVariableResult(querySearch, name, path);
                }
            }
        }
    }
}
