// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class WinAppSandboxPayload
{
    public static string Create(string productArchive, string payloadRoot, string toolsRoot, string destination)
    {
        RequirePlainPath(productArchive);
        RequirePlainPath(Path.Combine(toolsRoot, "winapp.exe"));
        RequirePlainPath(destination);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new WinAppSandboxException("guest_payload_preexisting");
        }

        Directory.CreateDirectory(destination);
        try
        {
            CopyDirectory(payloadRoot, Path.Combine(destination, "Payload"), tools: false);
            CopyDirectory(toolsRoot, Path.Combine(destination, "Tools"), tools: true);
            var runtime = Path.Combine(destination, "Runtime");
            Directory.CreateDirectory(runtime);
            File.Copy(productArchive, Path.Combine(runtime, "product.zip"), overwrite: false);
            return destination;
        }
        catch
        {
            _ = PlainFiles(destination).ToArray();
            Directory.Delete(destination, recursive: true);
            throw;
        }
    }

    public static IEnumerable<string> PlainFiles(string root)
    {
        RequirePlainPath(root);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new WinAppSandboxException("private_path_reparse_point");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    public static void RequirePlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new WinAppSandboxException("private_path_reparse_point");
            }
        }
    }

    private static void CopyDirectory(string source, string destination, bool tools)
    {
        foreach (var file in PlainFiles(source))
        {
            if (tools && Path.GetExtension(file).ToLowerInvariant() is not (".exe" or ".dll" or ".json" or ".config" or ".pri" or ".mui" or ".dat" or ".bin"))
            {
                continue;
            }

            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }
}
