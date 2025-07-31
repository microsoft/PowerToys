using System.Collections.Generic;

namespace TrimmingAnalyzer.Models
{
    public class TypeInfo
    {
        public string FullName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool IsSealed { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsInterface { get; set; }
        public bool IsEnum { get; set; }
        public bool IsDelegate { get; set; }
        public string? BaseType { get; set; }
        public List<string> Interfaces { get; set; } = new();
        public int MemberCount { get; set; }
    }
}