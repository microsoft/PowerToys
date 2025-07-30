using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: dotnet run -- <CSharpSourceFolder> <BinFolderWithReferences>");
            return;
        }

        string sourceFolder = args[0];
        string refsFolder = args[1];

        if (!Directory.Exists(sourceFolder) || !Directory.Exists(refsFolder))
        {
            Console.WriteLine("Invalid folder path(s).");
            return;
        }

        Console.WriteLine($"Analyzing source: {sourceFolder}");
        Console.WriteLine($"Using references: {refsFolder}");

        var trees = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();

        var references = Directory.GetFiles(refsFolder, "*.dll")
            .Select(dll => MetadataReference.CreateFromFile(dll))
            .ToList();

        var compilation = CSharpCompilation.Create("TypeAnalysis", trees, references);

        var typesUsed = new HashSet<string>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes())
            {
                ISymbol? symbol = node switch
                {
                    IdentifierNameSyntax ins => model.GetSymbolInfo(ins).Symbol,
                    ObjectCreationExpressionSyntax oces => model.GetTypeInfo(oces).Type,
                    TypeOfExpressionSyntax toes => model.GetTypeInfo(toes.Type).Type,
                    BaseTypeSyntax bts => model.GetTypeInfo(bts.Type).Type,
                    _ => null
                };

                if (symbol is INamedTypeSymbol typeSymbol && typeSymbol.TypeKind != TypeKind.Error)
                {
                    typesUsed.Add(typeSymbol.ToDisplayString());
                }
            }
        }

        Console.WriteLine("\n✅ Referenced types:");
        foreach (var type in typesUsed.OrderBy(t => t))
        {
            Console.WriteLine("- " + type);
        }
    }
}
