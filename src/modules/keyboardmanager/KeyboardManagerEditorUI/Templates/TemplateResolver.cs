// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public static class TemplateResolver
    {
        public readonly record struct Resolved(string Executable, string Args);

        public static Resolved Resolve(
            CommandTemplate template,
            IReadOnlyDictionary<string, string>? values)
        {
            var args = template.ArgsTemplate ?? string.Empty;

            foreach (var p in template.Parameters)
            {
                string replacement = string.Empty;
                if (values is not null && values.TryGetValue(p.Name, out var v))
                {
                    replacement = v ?? string.Empty;
                }

                args = args.Replace("{" + p.Name + "}", replacement);
            }

            return new Resolved(template.Executable ?? string.Empty, args);
        }
    }
}
