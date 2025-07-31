using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using TrimmingAnalyzer.Models;

namespace TrimmingAnalyzer
{
    public class TypeAnalyzer
    {
        public List<Models.TypeInfo> GetRemovedTypes(string untrimmedPath, string trimmedPath)
        {
            if (!File.Exists(untrimmedPath))
            {
                throw new FileNotFoundException($"Untrimmed assembly not found: {untrimmedPath}");
            }

            if (!File.Exists(trimmedPath))
            {
                throw new FileNotFoundException($"Trimmed assembly not found: {trimmedPath}");
            }

            var removedTypes = new List<Models.TypeInfo>();

            var untrimmedContext = new AssemblyLoadContext("Untrimmed", true);
            var trimmedContext = new AssemblyLoadContext("Trimmed", true);

            try
            {
                var untrimmedAssembly = untrimmedContext.LoadFromAssemblyPath(untrimmedPath);
                var trimmedAssembly = trimmedContext.LoadFromAssemblyPath(trimmedPath);

                var untrimmedTypes = untrimmedAssembly.GetTypes().Where(t => t.FullName != null).ToDictionary(t => t.FullName!);
                var trimmedTypeNames = trimmedAssembly.GetTypes().Where(t => t.FullName != null).Select(t => t.FullName!).ToHashSet();

                foreach (var kvp in untrimmedTypes)
                {
                    if (!trimmedTypeNames.Contains(kvp.Key))
                    {
                        var type = kvp.Value;
                        var typeInfo = new Models.TypeInfo
                        {
                            FullName = type.FullName ?? string.Empty,
                            Namespace = type.Namespace ?? "Global",
                            Name = type.Name,
                            IsPublic = type.IsPublic,
                            IsSealed = type.IsSealed,
                            IsAbstract = type.IsAbstract,
                            IsInterface = type.IsInterface,
                            IsEnum = type.IsEnum,
                            IsDelegate = type.IsSubclassOf(typeof(Delegate)),
                            BaseType = type.BaseType?.FullName,
                            Interfaces = type.GetInterfaces().Select(i => i.FullName ?? string.Empty).ToList(),
                            MemberCount = type.GetMembers(
                                BindingFlags.Public | BindingFlags.NonPublic |
                                BindingFlags.Instance | BindingFlags.Static |
                                BindingFlags.DeclaredOnly).Length,
                        };

                        removedTypes.Add(typeInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error analyzing assemblies: {ex.Message}", ex);
            }
            finally
            {
                untrimmedContext.Unload();
                trimmedContext.Unload();
            }

            return removedTypes.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToList();
        }
    }
}