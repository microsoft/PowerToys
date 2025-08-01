using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TrimmingAnalyzer.Models;

namespace TrimmingAnalyzer
{
    public class ReportGenerator
    {
        public void GenerateRdXml(List<TypeInfo> removedTypes, string outputPath)
        {
            var typesByNamespace = removedTypes.GroupBy(t => t.Namespace);
            
            // Define the namespace
            XNamespace ns = "http://schemas.microsoft.com/netfx/2013/01/metadata";
            
            var doc = new XDocument(
                new XElement(ns + "Directives",
                    new XElement(ns + "Application",
                        new XComment($"CmdPal Trimming Report - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
                        new XComment($"Total types trimmed: {removedTypes.Count}"),
                        new XComment("TrimMode: partial (as configured in Microsoft.CmdPal.UI.csproj)"),
                        new XElement(ns + "Assembly",
                            new XAttribute("Name", "Microsoft.CmdPal.UI"),
                            new XAttribute("Dynamic", "Required All"),
                            typesByNamespace.Select(g =>
                                new XElement(ns + "Namespace",
                                    new XAttribute("Name", g.Key),
                                    new XAttribute("Preserve", "All"),
                                    new XAttribute("Dynamic", "Required All"),
                                    g.Select(type =>
                                        new XElement(ns + "Type",
                                            new XAttribute("Name", type.Name),
                                            new XAttribute("Dynamic", "Required All"),
                                            new XAttribute("Serialize", "All"),
                                            new XAttribute("DataContractSerializer", "All"),
                                            new XAttribute("DataContractJsonSerializer", "All"),
                                            new XAttribute("XmlSerializer", "All"),
                                            new XAttribute("MarshalObject", "All"),
                                            new XAttribute("MarshalDelegate", "All")
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            doc.Save(outputPath);
        }

        public void GenerateMarkdown(List<TypeInfo> removedTypes, string outputPath)
        {
            GenerateMarkdown(removedTypes, outputPath, null);
        }

        public void GenerateMarkdown(List<TypeInfo> removedTypes, string outputPath, List<string>? assemblyNames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CmdPal Debug vs AOT Release Comparison Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"**Comparison:** Debug Build (no AOT) vs Release Build (with AOT)");
            sb.AppendLine($"**Purpose:** Show types removed when enabling AOT compilation in Release mode");
            sb.AppendLine();
            
            if (assemblyNames != null && assemblyNames.Count > 0)
            {
                sb.AppendLine($"**Analyzed assemblies:** {string.Join(", ", assemblyNames.Distinct().OrderBy(x => x))}");
                sb.AppendLine();
            }
            
            sb.AppendLine($"**Total types removed by AOT:** {removedTypes.Count}");
            sb.AppendLine();
            
            // Summary by namespace
            sb.AppendLine("## Summary by Namespace");
            sb.AppendLine();
            sb.AppendLine("| Namespace | Types Trimmed |");
            sb.AppendLine("|-----------|---------------|");
            
            foreach (var group in removedTypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key))
            {
                sb.AppendLine($"| `{group.Key}` | {group.Count()} |");
            }
            
            sb.AppendLine();
            sb.AppendLine("## Detailed Type List");
            sb.AppendLine();
            
            foreach (var group in removedTypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {group.Key}");
                sb.AppendLine();
                sb.AppendLine("| Type | Kind | Visibility | Base Type | Interfaces | Members |");
                sb.AppendLine("|------|------|------------|-----------|------------|---------|");
                
                foreach (var type in group.OrderBy(t => t.Name))
                {
                    var kind = GetTypeKind(type);
                    var visibility = type.IsPublic ? "Public" : "Internal";
                    var baseType = string.IsNullOrEmpty(type.BaseType) ? "-" : $"`{type.BaseType.Split('.').Last()}`";
                    var interfaces = type.Interfaces.Any() 
                        ? string.Join(", ", type.Interfaces.Take(3).Select(i => $"`{i.Split('.').Last()}`")) + 
                          (type.Interfaces.Count > 3 ? "..." : "")
                        : "-";
                    
                    sb.AppendLine($"| `{type.Name}` | {kind} | {visibility} | {baseType} | {interfaces} | {type.MemberCount} |");
                }
                sb.AppendLine();
            }
            
            // Add usage instructions
            sb.AppendLine("## How to Use This Report");
            sb.AppendLine();
            sb.AppendLine("If you need to preserve any of these types from trimming:");
            sb.AppendLine();
            sb.AppendLine("1. Copy the relevant entries from `TrimmedTypes.rd.xml` to your project's `rd.xml` file");
            sb.AppendLine("2. Or use `[DynamicallyAccessedMembers]` attributes in your code");
            sb.AppendLine("3. Or use `[DynamicDependency]` attributes to preserve specific members");
            sb.AppendLine();
            sb.AppendLine("Note: This report shows types that are present in Debug builds but removed in AOT Release builds.");
            sb.AppendLine("AOT compilation removes unused types and members to reduce binary size and improve startup performance.");
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, sb.ToString());
        }

        public void GenerateJson(List<TypeInfo> removedTypes, string outputPath, string assemblyName)
        {
            var analysisResult = new
            {
                AssemblyName = assemblyName,
                GeneratedAt = DateTime.Now,
                TotalTypes = removedTypes.Count,
                RemovedTypes = removedTypes.OrderBy(t => t.FullName).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(analysisResult, options);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, json);
        }

        private string GetTypeKind(TypeInfo type)
        {
            if (type.IsInterface) return "Interface";
            if (type.IsEnum) return "Enum";
            if (type.IsDelegate) return "Delegate";
            if (type.IsAbstract) return "Abstract Class";
            if (type.IsSealed) return "Sealed Class";
            return "Class";
        }
    }
}