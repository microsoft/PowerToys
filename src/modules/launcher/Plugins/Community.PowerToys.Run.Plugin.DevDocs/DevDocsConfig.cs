// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.DevDocs
{
    public class DevDocsConfig
    {
        public static string DevDocsApiUrl { get; } = "https://devdocs.io/docs.json";

        public static string DocumentsBaseUrl { get; } = "https://documents.devdocs.io";

        public static Dictionary<string, string> AliasMap { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // Languages
            ["c++"] = "cpp",
            ["py"] = "python",
            ["rb"] = "ruby",
            ["rs"] = "rust",
            ["golang"] = "go",
            ["kt"] = "kotlin",
            ["pl"] = "perl",
            ["hs"] = "haskell",
            ["sh"] = "bash",
            ["md"] = "markdown",

            // Frontend
            ["js"] = "javascript",
            ["ts"] = "typescript",
            ["ng"] = "angular",
            ["jq"] = "jquery",
            ["bs"] = "bootstrap",
            ["tw"] = "tailwindcss",

            ["next"] = "nextjs",
            ["node"] = "node",
            ["nodejs"] = "node",
            ["rn"] = "react_native",
            ["rr"] = "react_router",
            ["vr"] = "Vue Router",
            ["gl"] = "OpenGL",

            // Backend
            ["dj"] = "django",
            ["drf"] = "django_rest_framework",
            ["sf"] = "symfony",
            ["spring"] = "spring_boot",
            ["exp"] = "express",

            // Databases
            ["sql"] = "sqlite",
            ["pg"] = "postgresql",
            ["psql"] = "postgresql",
            ["postgres"] = "postgresql",
            ["maria"] = "mariadb",
        };
    }
}
