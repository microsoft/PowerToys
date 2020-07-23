using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Plugin.VSCodeWorkspaces.SshConfigParser
{
    internal static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (var element in collection)
            {
                action(element);
            }
        }
    }
}