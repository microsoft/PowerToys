using System;
using System.IO;

namespace TrimmingAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: TrimmingAnalyzer <untrimmed.dll> <trimmed.dll> <output-dir> [formats]");
                Console.WriteLine("Formats: rdxml,markdown (default: rdxml,markdown)");
                return;
            }

            var untrimmedPath = Path.GetFullPath(args[0]);
            var trimmedPath = Path.GetFullPath(args[1]);
            var outputDir = Path.GetFullPath(args[2]);
            var formats = args.Length > 3 ? args[3].Split(',') : new[] { "rdxml", "markdown" };

            try
            {
                Console.WriteLine("Analyzing assemblies...");
                var analyzer = new TypeAnalyzer();
                var removedTypes = analyzer.GetRemovedTypes(untrimmedPath, trimmedPath);

                Console.WriteLine($"Found {removedTypes.Count} trimmed types");

                var generator = new ReportGenerator();

                foreach (var format in formats)
                {
                    switch (format.Trim().ToLower())
                    {
                        case "rdxml":
                            var rdxmlPath = Path.Combine(outputDir, "TrimmedTypes.rd.xml");
                            generator.GenerateRdXml(removedTypes, rdxmlPath);
                            Console.WriteLine($"Generated: {rdxmlPath}");
                            break;
                        case "markdown":
                            var markdownPath = Path.Combine(outputDir, "TrimmedTypes.md");
                            generator.GenerateMarkdown(removedTypes, markdownPath);
                            Console.WriteLine($"Generated: {markdownPath}");
                            break;
                        default:
                            Console.WriteLine($"Unknown format: {format}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}