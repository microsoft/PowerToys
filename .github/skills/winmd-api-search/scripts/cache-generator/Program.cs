// Standalone WinMD cache generator — per-package deduplicate, multi-project support.
// Parses WinMD files from NuGet packages and Windows SDK, exports JSON cache
// keyed by package+version to avoid duplication across projects.
//
// Usage:
//   CacheGenerator <project-dir> <output-dir>
//   CacheGenerator --scan <root-dir> <output-dir>

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

// --- Arg parsing ---

var scanMode = args.Contains("--scan");
var positionalArgs = args.Where(a => !a.StartsWith('-')).ToArray();

if (positionalArgs.Length < 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  CacheGenerator <project-dir> <output-dir>");
    Console.Error.WriteLine("  CacheGenerator --scan <root-dir> <output-dir>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  project-dir: Path containing .csproj/.vcxproj (or a project file itself)");
    Console.Error.WriteLine("  root-dir:    Root to scan recursively for project files");
    Console.Error.WriteLine("  output-dir:  Cache output (e.g. \"Generated Files\\winmd-cache\")");
    return 1;
}

var inputPath = Path.GetFullPath(positionalArgs[0]);
var outputDir = Path.GetFullPath(positionalArgs[1]);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
};

// --- Discover project files ---

var projectFiles = new List<string>();

if (scanMode)
{
    if (!Directory.Exists(inputPath))
    {
        Console.Error.WriteLine($"Error: Root directory not found: {inputPath}");
        return 1;
    }

    projectFiles.AddRange(Directory.GetFiles(inputPath, "*.csproj", SearchOption.AllDirectories));
    projectFiles.AddRange(Directory.GetFiles(inputPath, "*.vcxproj", SearchOption.AllDirectories));

    // Exclude common non-source directories
    projectFiles = projectFiles
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToList();
}
else if (File.Exists(inputPath) && (inputPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                     inputPath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase)))
{
    projectFiles.Add(inputPath);
}
else if (Directory.Exists(inputPath))
{
    projectFiles.AddRange(Directory.GetFiles(inputPath, "*.csproj"));
    projectFiles.AddRange(Directory.GetFiles(inputPath, "*.vcxproj"));
}
else
{
    Console.Error.WriteLine($"Error: Path not found: {inputPath}");
    return 1;
}

if (projectFiles.Count == 0)
{
    Console.Error.WriteLine($"No .csproj or .vcxproj files found in: {inputPath}");
    return 1;
}

// Always include CacheGenerator.csproj as a baseline source of WinAppSDK WinMD files.
// It references Microsoft.WindowsAppSDK with ExcludeAssets="all" so the packages are
// downloaded during restore/build but don't affect the tool's compilation.
var selfCsproj = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "CacheGenerator.csproj");
selfCsproj = Path.GetFullPath(selfCsproj);
if (File.Exists(selfCsproj) && !projectFiles.Any(f =>
    Path.GetFullPath(f).Equals(selfCsproj, StringComparison.OrdinalIgnoreCase)))
{
    projectFiles.Add(selfCsproj);
}

Console.WriteLine($"WinMD Cache Generator (per-package deduplicate)");
Console.WriteLine($"  Output:   {outputDir}");
Console.WriteLine($"  Projects: {projectFiles.Count}");

// --- Process each project ---

var totalPackagesCached = 0;
var totalPackagesSkipped = 0;
var totalProjectsProcessed = 0;

foreach (var projectFile in projectFiles)
{
    var projectDir = Path.GetDirectoryName(projectFile)!;
    var projectName = Path.GetFileNameWithoutExtension(projectFile);

    Console.WriteLine($"\n--- {projectName} ({Path.GetFileName(projectFile)}) ---");

    // Find packages that contain WinMD files
    var packages = NuGetResolver.FindPackagesWithWinMd(projectDir, projectFile);

    if (packages.Count == 0)
    {
        Console.WriteLine("  No packages with WinMD files (is the project restored?)");
        continue;
    }

    Console.WriteLine($"  {packages.Count} package(s) with WinMD files");
    totalProjectsProcessed++;

    var projectPackages = new List<ProjectPackageRef>();

    foreach (var pkg in packages)
    {
        var pkgCacheDir = Path.Combine(outputDir, "packages", pkg.Id, pkg.Version);
        var metaPath = Path.Combine(pkgCacheDir, "meta.json");

        if (File.Exists(metaPath))
        {
            Console.WriteLine($"  [cached] {pkg.Id}@{pkg.Version}");
            totalPackagesSkipped++;
        }
        else
        {
            Console.WriteLine($"  [parse]  {pkg.Id}@{pkg.Version} ({pkg.WinMdFiles.Count} WinMD file(s))");
            ExportPackageCache(pkg, pkgCacheDir);
            totalPackagesCached++;
        }

        projectPackages.Add(new ProjectPackageRef { Id = pkg.Id, Version = pkg.Version });
    }

    // Write project manifest
    var manifest = new ProjectManifest
    {
        ProjectName = projectName,
        ProjectDir = projectDir,
        ProjectFile = Path.GetFileName(projectFile),
        Packages = projectPackages,
        GeneratedAt = DateTime.UtcNow.ToString("o"),
    };

    var projectsDir = Path.Combine(outputDir, "projects");
    Directory.CreateDirectory(projectsDir);
    File.WriteAllText(
        Path.Combine(projectsDir, $"{projectName}.json"),
        JsonSerializer.Serialize(manifest, jsonOptions));
}

Console.WriteLine($"\nDone: {totalProjectsProcessed} project(s) processed, " +
                  $"{totalPackagesCached} package(s) parsed, " +
                  $"{totalPackagesSkipped} reused from cache");
return 0;

// =============================================================================
// Export a single package's WinMD data to cache
// =============================================================================

void ExportPackageCache(PackageWithWinMd pkg, string cacheDir)
{
    var typesDir = Path.Combine(cacheDir, "types");
    Directory.CreateDirectory(typesDir);

    var allTypes = new List<WinMdTypeInfo>();
    foreach (var file in pkg.WinMdFiles)
    {
        allTypes.AddRange(WinMdParser.ParseFile(file));
    }

    var typesByNamespace = allTypes
        .GroupBy(t => t.Namespace)
        .ToDictionary(g => g.Key, g => g.ToList());

    var namespaces = typesByNamespace.Keys
        .Where(ns => !string.IsNullOrEmpty(ns))
        .OrderBy(ns => ns)
        .ToList();

    // meta.json
    var meta = new
    {
        PackageId = pkg.Id,
        Version = pkg.Version,
        WinMdFiles = pkg.WinMdFiles.Select(Path.GetFileName).Distinct().ToList(),
        TotalTypes = allTypes.Count,
        TotalMembers = allTypes.Sum(t => t.Members.Count),
        TotalNamespaces = namespaces.Count,
        GeneratedAt = DateTime.UtcNow.ToString("o"),
    };

    File.WriteAllText(
        Path.Combine(cacheDir, "meta.json"),
        JsonSerializer.Serialize(meta, jsonOptions));

    // namespaces.json
    File.WriteAllText(
        Path.Combine(cacheDir, "namespaces.json"),
        JsonSerializer.Serialize(namespaces, jsonOptions));

    // types/<Namespace>.json
    foreach (var ns in namespaces)
    {
        var types = typesByNamespace[ns];
        var safeFileName = ns.Replace('.', '_') + ".json";
        File.WriteAllText(
            Path.Combine(typesDir, safeFileName),
            JsonSerializer.Serialize(types, jsonOptions));
    }
}

// =============================================================================
// Data Models
// =============================================================================

enum TypeKind { Class, Struct, Enum, Interface, Delegate }

enum MemberKind { Method, Property, Event, Field }

sealed class WinMdTypeInfo
{
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required TypeKind Kind { get; init; }
    public string? BaseType { get; init; }
    public required List<WinMdMemberInfo> Members { get; init; }
    public List<string>? EnumValues { get; init; }
    public required string SourceFile { get; init; }
}

sealed class WinMdMemberInfo
{
    public required string Name { get; init; }
    public required MemberKind Kind { get; init; }
    public required string Signature { get; init; }
    public string? ReturnType { get; init; }
    public List<WinMdParameterInfo>? Parameters { get; init; }
}

sealed class WinMdParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

sealed class ProjectPackageRef
{
    public required string Id { get; init; }
    public required string Version { get; init; }
}

sealed class ProjectManifest
{
    public required string ProjectName { get; init; }
    public required string ProjectDir { get; init; }
    public required string ProjectFile { get; init; }
    public required List<ProjectPackageRef> Packages { get; init; }
    public required string GeneratedAt { get; init; }
}

// =============================================================================
// NuGet Resolver — finds packages with WinMD files, returns structured data
// =============================================================================

record PackageWithWinMd(string Id, string Version, List<string> WinMdFiles);

static class NuGetResolver
{
    public static List<PackageWithWinMd> FindPackagesWithWinMd(string projectDir, string projectFile)
    {
        var result = new List<PackageWithWinMd>();

        // 1. Try project.assets.json (PackageReference — .csproj and modern .vcxproj)
        var assetsPath = FindProjectAssetsJson(projectDir);
        if (assetsPath is not null)
        {
            result.AddRange(FindPackagesFromAssets(assetsPath));
        }

        // 2. Try packages.config (older .vcxproj / .csproj using NuGet packages.config)
        if (result.Count == 0)
        {
            var packagesConfig = Path.Combine(projectDir, "packages.config");
            if (File.Exists(packagesConfig))
            {
                result.AddRange(FindPackagesFromConfig(packagesConfig, projectDir));
            }
        }

        // 3. Project references — parse <ProjectReference> from .csproj/.vcxproj XML,
        //    then check each referenced project's bin/ for .winmd build output.
        //    This is the reliable way to find class libraries that generate WinMD.
        result.AddRange(FindWinMdFromProjectReferences(projectFile));

        // 4. Windows SDK as a synthetic "package"
        var sdkWinMd = FindWindowsSdkWinMd();
        if (sdkWinMd.Files.Count > 0)
        {
            result.Add(new PackageWithWinMd("WindowsSDK", sdkWinMd.Version, sdkWinMd.Files));
        }

        return result;
    }

    /// <summary>
    /// Parse &lt;ProjectReference&gt; from .csproj/.vcxproj and find .winmd output
    /// from each referenced project's bin/ directory.
    /// </summary>
    internal static List<PackageWithWinMd> FindWinMdFromProjectReferences(string projectFile)
    {
        var result = new List<PackageWithWinMd>();

        try
        {
            var doc = XDocument.Load(projectFile);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var projectRefs = doc.Descendants(ns + "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => v is not null)
                .ToList();

            if (projectRefs.Count == 0)
            {
                return result;
            }

            var projectDir = Path.GetDirectoryName(projectFile)!;

            foreach (var refPath in projectRefs)
            {
                var refFullPath = Path.GetFullPath(Path.Combine(projectDir, refPath!));
                if (!File.Exists(refFullPath))
                {
                    continue;
                }

                var refProjectDir = Path.GetDirectoryName(refFullPath)!;
                var refProjectName = Path.GetFileNameWithoutExtension(refFullPath);
                var refBinDir = Path.Combine(refProjectDir, "bin");

                if (!Directory.Exists(refBinDir))
                {
                    continue;
                }

                var winmdFiles = Directory.GetFiles(refBinDir, "*.winmd", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).Equals("Windows.winmd", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Deduplicate by filename (same WinMD across Debug/Release/x64/etc.)
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                winmdFiles = winmdFiles
                    .Where(f => seen.Add(Path.GetFileName(f)))
                    .ToList();

                if (winmdFiles.Count > 0)
                {
                    result.Add(new PackageWithWinMd($"ProjectRef.{refProjectName}", "local", winmdFiles));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse project references: {ex.Message}");
        }

        return result;
    }

    internal static string? FindProjectAssetsJson(string projectDir)
    {
        // Standard location
        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (File.Exists(assetsPath))
        {
            return assetsPath;
        }

        // Sometimes under platform-specific subdirectories
        var objDir = Path.Combine(projectDir, "obj");
        if (Directory.Exists(objDir))
        {
            var found = Directory.GetFiles(objDir, "project.assets.json", SearchOption.AllDirectories);
            if (found.Length > 0)
            {
                return found[0];
            }
        }

        return null;
    }

    internal static List<PackageWithWinMd> FindPackagesFromAssets(string assetsPath)
    {
        var result = new List<PackageWithWinMd>();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = doc.RootElement;

            var packageFolders = new List<string>();
            if (root.TryGetProperty("packageFolders", out var folders))
            {
                foreach (var folder in folders.EnumerateObject())
                {
                    packageFolders.Add(folder.Name);
                }
            }

            if (!root.TryGetProperty("libraries", out var libraries))
            {
                return result;
            }

            foreach (var lib in libraries.EnumerateObject())
            {
                // Key format: "PackageId/Version"
                var slashIdx = lib.Name.IndexOf('/');
                if (slashIdx < 0)
                {
                    continue;
                }

                var packageId = lib.Name[..slashIdx];
                var version = lib.Name[(slashIdx + 1)..];

                if (!lib.Value.TryGetProperty("path", out var pathProp))
                {
                    continue;
                }

                var libPath = pathProp.GetString();
                if (libPath is null)
                {
                    continue;
                }

                var winmdFiles = new List<string>();
                foreach (var folder in packageFolders)
                {
                    var fullPath = Path.Combine(folder, libPath);
                    if (!Directory.Exists(fullPath))
                    {
                        continue;
                    }

                    winmdFiles.AddRange(
                        Directory.GetFiles(fullPath, "*.winmd", SearchOption.AllDirectories));
                }

                // Deduplicate by filename (WinMD is arch-neutral metadata)
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                winmdFiles = winmdFiles
                    .Where(f => seen.Add(Path.GetFileName(f)))
                    .ToList();

                if (winmdFiles.Count > 0)
                {
                    result.Add(new PackageWithWinMd(packageId, version, winmdFiles));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse project.assets.json: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parses packages.config (older NuGet format used by some .vcxproj and legacy .csproj).
    /// Looks for a solution-level "packages/" folder or the NuGet global cache.
    /// </summary>
    internal static List<PackageWithWinMd> FindPackagesFromConfig(string configPath, string projectDir)
    {
        var result = new List<PackageWithWinMd>();

        try
        {
            var doc = System.Xml.Linq.XDocument.Load(configPath);
            var packages = doc.Root?.Elements("package");
            if (packages is null)
            {
                return result;
            }

            // packages.config repos typically have a solution-level "packages/" folder.
            // Walk up from project dir to find it.
            var packagesFolder = FindSolutionPackagesFolder(projectDir);

            // Also check NuGet global packages cache
            var globalPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");

            foreach (var pkg in packages)
            {
                var id = pkg.Attribute("id")?.Value;
                var version = pkg.Attribute("version")?.Value;
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                {
                    continue;
                }

                var winmdFiles = new List<string>();

                // Check solution-level packages/ folder (format: packages/<id>.<version>/)
                if (packagesFolder is not null)
                {
                    var pkgDir = Path.Combine(packagesFolder, $"{id}.{version}");
                    if (Directory.Exists(pkgDir))
                    {
                        winmdFiles.AddRange(
                            Directory.GetFiles(pkgDir, "*.winmd", SearchOption.AllDirectories));
                    }
                }

                // Fallback: NuGet global cache (format: <id>/<version>/)
                if (winmdFiles.Count == 0 && Directory.Exists(globalPackages))
                {
                    var pkgDir = Path.Combine(globalPackages, id.ToLowerInvariant(), version);
                    if (Directory.Exists(pkgDir))
                    {
                        winmdFiles.AddRange(
                            Directory.GetFiles(pkgDir, "*.winmd", SearchOption.AllDirectories));
                    }
                }

                // Deduplicate by filename
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                winmdFiles = winmdFiles
                    .Where(f => seen.Add(Path.GetFileName(f)))
                    .ToList();

                if (winmdFiles.Count > 0)
                {
                    result.Add(new PackageWithWinMd(id, version, winmdFiles));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse packages.config: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Walk up from project dir to find a solution-level "packages/" folder.
    /// </summary>
    internal static string? FindSolutionPackagesFolder(string startDir)
    {
        var dir = startDir;
        for (var i = 0; i < 5; i++) // Walk up at most 5 levels
        {
            var packagesDir = Path.Combine(dir, "packages");
            if (Directory.Exists(packagesDir))
            {
                return packagesDir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return null;
    }

    internal static (List<string> Files, string Version) FindWindowsSdkWinMd()
    {
        var windowsKitsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "UnionMetadata");

        if (!Directory.Exists(windowsKitsPath))
        {
            return ([], "unknown");
        }

        // Filter to version-numbered directories only (skip "Facade" etc.)
        var versionDirs = Directory.GetDirectories(windowsKitsPath)
            .Where(d => char.IsDigit(Path.GetFileName(d)[0]))
            .OrderByDescending(d => Path.GetFileName(d))
            .ToList();

        foreach (var versionDir in versionDirs)
        {
            var windowsWinMd = Path.Combine(versionDir, "Windows.winmd");
            if (File.Exists(windowsWinMd))
            {
                var version = Path.GetFileName(versionDir);
                return ([windowsWinMd], version);
            }
        }

        return ([], "unknown");
    }
}

// =============================================================================
// Signature Type Provider — decodes metadata signatures to readable strings
// =============================================================================

sealed class SimpleTypeProvider : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "Boolean",
        PrimitiveTypeCode.Byte => "Byte",
        PrimitiveTypeCode.SByte => "SByte",
        PrimitiveTypeCode.Char => "Char",
        PrimitiveTypeCode.Int16 => "Int16",
        PrimitiveTypeCode.UInt16 => "UInt16",
        PrimitiveTypeCode.Int32 => "Int32",
        PrimitiveTypeCode.UInt32 => "UInt32",
        PrimitiveTypeCode.Int64 => "Int64",
        PrimitiveTypeCode.UInt64 => "UInt64",
        PrimitiveTypeCode.Single => "Single",
        PrimitiveTypeCode.Double => "Double",
        PrimitiveTypeCode.String => "String",
        PrimitiveTypeCode.Object => "Object",
        PrimitiveTypeCode.Void => "void",
        PrimitiveTypeCode.IntPtr => "IntPtr",
        PrimitiveTypeCode.UIntPtr => "UIntPtr",
        PrimitiveTypeCode.TypedReference => "TypedReference",
        _ => typeCode.ToString(),
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var name = reader.GetString(typeDef.Name);
        var ns = reader.GetString(typeDef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);
        var name = reader.GetString(typeRef.Name);
        var ns = reader.GetString(typeRef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    public string GetArrayType(string elementType, ArrayShape shape) =>
        $"{elementType}[{new string(',', shape.Rank - 1)}]";

    public string GetByReferenceType(string elementType) => $"ref {elementType}";
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPinnedType(string elementType) => elementType;

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var name = genericType;
        var backtick = name.IndexOf('`');
        if (backtick >= 0)
        {
            name = name[..backtick];
        }

        return $"{name}<{string.Join(", ", typeArguments)}>";
    }

    public string GetGenericMethodParameter(object? genericContext, int index) => $"TMethod{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"T{index}";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext,
        TypeSpecificationHandle handle, byte rawTypeKind)
    {
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}

// =============================================================================
// WinMD Parser — reads WinMD files into structured type info
// =============================================================================

static class WinMdParser
{
    public static List<WinMdTypeInfo> ParseFile(string filePath)
    {
        var types = new List<WinMdTypeInfo>();

        try
        {
            using var stream = File.OpenRead(filePath);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return types;
            }

            var reader = peReader.GetMetadataReader();
            var typeProvider = new SimpleTypeProvider();

            foreach (var typeDefHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeDefHandle);
                var name = reader.GetString(typeDef.Name);
                var ns = reader.GetString(typeDef.Namespace);

                if (ShouldSkipType(name, typeDef))
                {
                    continue;
                }

                var kind = DetermineTypeKind(reader, typeDef);
                var baseType = GetBaseTypeName(reader, typeDef);
                var members = ParseMembers(reader, typeDef, typeProvider);
                var enumValues = kind == TypeKind.Enum ? ParseEnumValues(reader, typeDef) : null;
                var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                types.Add(new WinMdTypeInfo
                {
                    Namespace = ns,
                    Name = name,
                    FullName = fullName,
                    Kind = kind,
                    BaseType = baseType,
                    Members = members,
                    EnumValues = enumValues,
                    SourceFile = Path.GetFileName(filePath),
                });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse {filePath}: {ex.Message}");
        }

        return types;
    }

    internal static bool ShouldSkipType(string name, TypeDefinition typeDef)
    {
        if (string.IsNullOrEmpty(name) || name == "<Module>" || name.StartsWith('<'))
        {
            return true;
        }

        var visibility = typeDef.Attributes & TypeAttributes.VisibilityMask;
        return visibility != TypeAttributes.Public && visibility != TypeAttributes.NestedPublic;
    }

    internal static TypeKind DetermineTypeKind(MetadataReader reader, TypeDefinition typeDef)
    {
        if ((typeDef.Attributes & TypeAttributes.Interface) != 0)
        {
            return TypeKind.Interface;
        }

        var baseType = GetBaseTypeName(reader, typeDef);
        return baseType switch
        {
            "System.Enum" => TypeKind.Enum,
            "System.ValueType" => TypeKind.Struct,
            "System.MulticastDelegate" or "System.Delegate" => TypeKind.Delegate,
            _ => TypeKind.Class,
        };
    }

    private static string? GetBaseTypeName(MetadataReader reader, TypeDefinition typeDef)
    {
        if (typeDef.BaseType.IsNil)
        {
            return null;
        }

        return typeDef.BaseType.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeDefName(reader, (TypeDefinitionHandle)typeDef.BaseType),
            HandleKind.TypeReference => GetTypeRefName(reader, (TypeReferenceHandle)typeDef.BaseType),
            _ => null,
        };
    }

    private static string GetTypeDefName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var td = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(td.Namespace);
        var name = reader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string GetTypeRefName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = reader.GetString(tr.Namespace);
        var name = reader.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static List<WinMdMemberInfo> ParseMembers(
        MetadataReader reader, TypeDefinition typeDef, SimpleTypeProvider typeProvider)
    {
        var members = new List<WinMdMemberInfo>();

        // Collect property/event accessor methods so we can skip them in the methods loop
        var accessorMethods = new HashSet<MethodDefinitionHandle>();
        foreach (var propHandle in typeDef.GetProperties())
        {
            var accessors = reader.GetPropertyDefinition(propHandle).GetAccessors();
            if (!accessors.Getter.IsNil) accessorMethods.Add(accessors.Getter);
            if (!accessors.Setter.IsNil) accessorMethods.Add(accessors.Setter);
        }

        foreach (var eventHandle in typeDef.GetEvents())
        {
            var accessors = reader.GetEventDefinition(eventHandle).GetAccessors();
            if (!accessors.Adder.IsNil) accessorMethods.Add(accessors.Adder);
            if (!accessors.Remover.IsNil) accessorMethods.Add(accessors.Remover);
            if (!accessors.Raiser.IsNil) accessorMethods.Add(accessors.Raiser);
        }

        // Methods
        foreach (var methodHandle in typeDef.GetMethods())
        {
            if (accessorMethods.Contains(methodHandle))
            {
                continue;
            }

            var method = reader.GetMethodDefinition(methodHandle);
            var methodName = reader.GetString(method.Name);

            if (methodName.StartsWith('.') || methodName.StartsWith('<'))
            {
                continue;
            }

            if ((method.Attributes & MethodAttributes.Public) == 0)
            {
                continue;
            }

            try
            {
                var sig = method.DecodeSignature(typeProvider, null);
                var parameters = GetMethodParameters(reader, method, sig);
                var paramStr = string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));

                members.Add(new WinMdMemberInfo
                {
                    Name = methodName,
                    Kind = MemberKind.Method,
                    Signature = $"{sig.ReturnType} {methodName}({paramStr})",
                    ReturnType = sig.ReturnType,
                    Parameters = parameters,
                });
            }
            catch
            {
                members.Add(new WinMdMemberInfo
                {
                    Name = methodName,
                    Kind = MemberKind.Method,
                    Signature = $"{methodName}(/* signature not decodable */)",
                });
            }
        }

        // Properties
        foreach (var propHandle in typeDef.GetProperties())
        {
            var prop = reader.GetPropertyDefinition(propHandle);
            var propName = reader.GetString(prop.Name);

            try
            {
                var propSig = prop.DecodeSignature(typeProvider, null);
                var propType = propSig.ReturnType;
                var accessors = prop.GetAccessors();
                var hasGetter = !accessors.Getter.IsNil;
                var hasSetter = !accessors.Setter.IsNil;
                var accessStr = (hasGetter, hasSetter) switch
                {
                    (true, true) => "{ get; set; }",
                    (true, false) => "{ get; }",
                    (false, true) => "{ set; }",
                    _ => "{ }",
                };

                members.Add(new WinMdMemberInfo
                {
                    Name = propName,
                    Kind = MemberKind.Property,
                    Signature = $"{propType} {propName} {accessStr}",
                    ReturnType = propType,
                });
            }
            catch
            {
                members.Add(new WinMdMemberInfo
                {
                    Name = propName,
                    Kind = MemberKind.Property,
                    Signature = $"/* type not decodable */ {propName}",
                });
            }
        }

        // Events
        foreach (var eventHandle in typeDef.GetEvents())
        {
            var evt = reader.GetEventDefinition(eventHandle);
            var evtName = reader.GetString(evt.Name);
            var evtType = GetHandleTypeName(reader, evt.Type);

            members.Add(new WinMdMemberInfo
            {
                Name = evtName,
                Kind = MemberKind.Event,
                Signature = $"event {evtType} {evtName}",
                ReturnType = evtType,
            });
        }

        return members;
    }

    private static List<WinMdParameterInfo> GetMethodParameters(
        MetadataReader reader, MethodDefinition method, MethodSignature<string> sig)
    {
        var parameters = new List<WinMdParameterInfo>();
        var paramHandles = method.GetParameters().ToList();
        var paramNames = new List<string>();

        foreach (var ph in paramHandles)
        {
            var param = reader.GetParameter(ph);
            if (param.SequenceNumber > 0)
            {
                paramNames.Add(reader.GetString(param.Name));
            }
        }

        for (var i = 0; i < sig.ParameterTypes.Length; i++)
        {
            parameters.Add(new WinMdParameterInfo
            {
                Name = i < paramNames.Count ? paramNames[i] : $"arg{i}",
                Type = sig.ParameterTypes[i],
            });
        }

        return parameters;
    }

    internal static List<string> ParseEnumValues(MetadataReader reader, TypeDefinition typeDef)
    {
        var values = new List<string>();

        foreach (var fieldHandle in typeDef.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var fieldName = reader.GetString(field.Name);

            if (fieldName == "value__")
            {
                continue;
            }

            if ((field.Attributes & FieldAttributes.Public) != 0 &&
                (field.Attributes & FieldAttributes.Static) != 0)
            {
                values.Add(fieldName);
            }
        }

        return values;
    }

    private static string GetHandleTypeName(MetadataReader reader, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeDefName(reader, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => GetTypeRefName(reader, (TypeReferenceHandle)handle),
        _ => "unknown",
    };
}
