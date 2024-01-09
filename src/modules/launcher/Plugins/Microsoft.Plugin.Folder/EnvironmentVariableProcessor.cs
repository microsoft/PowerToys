// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder
{
    public class EnvironmentVariableProcessor : IFolderProcessor
    {
        private readonly IEnvironmentHelper _environmentHelper;
        private readonly IQueryEnvironmentVariable _queryEnvironmentVariable;

        public EnvironmentVariableProcessor(IEnvironmentHelper environmentHelper, IQueryEnvironmentVariable queryEnvironmentVariable)
        {
            _environmentHelper = environmentHelper;
            _queryEnvironmentVariable = queryEnvironmentVariable;
        }

        public IEnumerable<IItemResult> Results(string actionKeyword, string search)
        {
            ArgumentNullException.ThrowIfNull(search);

            if (!_environmentHelper.IsEnvironmentVariable(search))
            {
                return Enumerable.Empty<IItemResult>();
            }

            return _queryEnvironmentVariable.Query(search);
        }
    }
}
