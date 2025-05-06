// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

// TODO: ok, fine. Currently we implement it but not use it.
// Consider to remove it in the future.
public class EnvironmentsCache
{
    private static readonly Lazy<EnvironmentsCache> _instance = new Lazy<EnvironmentsCache>(() => new EnvironmentsCache());

    public static EnvironmentsCache Instance => _instance.Value;

    private Dictionary<string, string> _envVars;

    private List<string> _paths;

    private EnvironmentsCache()
    {
        _envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var envVars = Environment.GetEnvironmentVariables();

        // convert from envVars to Dictionary<string, string>
        foreach (DictionaryEntry entry in envVars)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                _envVars[key] = value;
            }
        }

        _paths = [];
        if (_envVars.TryGetValue("PATH", out var pathStr))
        {
            _paths = pathStr.Split(";").ToList();
        }
    }

    public bool TryGetValue(string key, out string result)
    {
        if (_envVars.TryGetValue(key, out var value))
        {
            result = value;
            return true;
        }

        result = string.Empty;

        return false;
    }

    public string GetValue(string key)
    {
        if (_envVars.TryGetValue(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    public List<string> GetPaths()
    {
        return _paths;
    }

    public bool TryGetExecutableFileFullPath(string fileName, out string fullPath)
    {
        foreach (var path in _paths)
        {
            fullPath = System.IO.Path.Combine(path, fileName);
            if (System.IO.File.Exists(fullPath))
            {
                return true;
            }
        }

        fullPath = string.Empty;
        return false;
    }
}
