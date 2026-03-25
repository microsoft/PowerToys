// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

internal sealed class ExtensionTemplateService : IExtensionTemplateService
{
    internal enum TemplateFileHandling
    {
        ReplaceTokens,
        CopyAsIs,
    }

    private const string TemplateArchiveRelativePath = "Microsoft.CmdPal.UI.ViewModels\\Assets\\template.zip";

    private static readonly HashSet<string> _replaceTokensTemplateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".appxmanifest",
        ".config",
        ".cs",
        ".csproj",
        ".json",
        ".manifest",
        ".props",
        ".pubxml",
        ".sln",
    };

    private static readonly HashSet<string> _copyAsIsTemplateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
    };

    private readonly string _templateZipPath;

    public ExtensionTemplateService()
        : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TemplateArchiveRelativePath))
    {
    }

    internal ExtensionTemplateService(string templateZipPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateZipPath);
        _templateZipPath = templateZipPath;
    }

    internal static IReadOnlyCollection<string> ReplaceTokensTemplateExtensions => _replaceTokensTemplateExtensions;

    internal static IReadOnlyCollection<string> CopyAsIsTemplateExtensions => _copyAsIsTemplateExtensions;

    public void CreateExtension(string extensionName, string displayName, string outputPath)
    {
        var newGuid = Guid.NewGuid().ToString();

        // Unzip `template.zip` to a temp dir:
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        ZipFile.ExtractToDirectory(_templateZipPath, tempDir);

        try
        {
            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                CopyTemplateFile(tempDir, file, outputPath, extensionName, displayName, newGuid);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    internal static void CopyTemplateFile(string templateRoot, string sourceFile, string outputPath, string extensionName, string displayName, string newGuid)
    {
        var relativePath = Path.GetRelativePath(templateRoot, sourceFile);
        var newFileName = Path.Combine(outputPath, relativePath).Replace("TemplateCmdPalExtension", extensionName, StringComparison.Ordinal);

        Directory.CreateDirectory(Path.GetDirectoryName(newFileName)!);

        switch (GetTemplateFileHandling(sourceFile))
        {
            case TemplateFileHandling.ReplaceTokens:
                var sourceText = File.ReadAllText(sourceFile);
                var updatedText = ReplaceTemplateTokens(sourceText, extensionName, displayName, newGuid);
                if (string.Equals(sourceText, updatedText, StringComparison.Ordinal))
                {
                    File.Copy(sourceFile, newFileName, overwrite: true);
                    break;
                }

                File.WriteAllText(newFileName, updatedText);
                break;
            case TemplateFileHandling.CopyAsIs:
            default:
                File.Copy(sourceFile, newFileName, overwrite: true);
                break;
        }
    }

    internal static TemplateFileHandling GetTemplateFileHandling(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (_replaceTokensTemplateExtensions.Contains(extension))
        {
            return TemplateFileHandling.ReplaceTokens;
        }

        if (_copyAsIsTemplateExtensions.Contains(extension))
        {
            return TemplateFileHandling.CopyAsIs;
        }

        throw new InvalidOperationException($"Template file '{filePath}' has unsupported extension '{extension}'. Update the template file handling lists in {nameof(ExtensionTemplateService)}.");
    }

    private static string ReplaceTemplateTokens(string text, string extensionName, string displayName, string newGuid) =>
        text
            .Replace("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", newGuid, StringComparison.Ordinal)
            .Replace("TemplateCmdPalExtension", extensionName, StringComparison.Ordinal)
            .Replace("TemplateDisplayName", displayName, StringComparison.Ordinal);
}
