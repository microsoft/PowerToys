// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.MouseWithoutBorders.SandboxExperiment
{
    public sealed class RuntimeAssemblyInfo
    {
        public string Name { get; set; }

        public string Identity { get; set; }

        public string Configuration { get; set; }

        public string Mvid { get; set; }

        public bool ReadyToRun { get; set; }

        public bool ILOnly { get; set; }

        public int MethodCount { get; set; }
    }

    public static class ReadyToRunMetadata
    {
        public static bool IsNativeX64(string path) => IsNativeArchitecture(path, "x64");

        // Host build tools (crossgen2.exe, its JIT dependencies) and the WinApp Sandbox preview
        // CLI are all native images; only the expected target Machine differs. ARM64EC (hybrid)
        // is deliberately not accepted here: a native ARM64 request must produce Machine.Arm64,
        // never the x64-compatible hybrid Machine.Arm64EC.
        public static bool IsNativeArchitecture(string path, string architecture)
        {
            var expected = ResolveMachine(architecture);
            using (var stream = File.OpenRead(path))
            using (var image = new PEReader(stream))
            {
                return image.PEHeaders.CoffHeader.Machine == expected &&
                    image.PEHeaders.CorHeader == null;
            }
        }

        private static Machine ResolveMachine(string architecture)
        {
            switch (architecture)
            {
                case "x64":
                    return Machine.Amd64;
                case "arm64":
                    return Machine.Arm64;
                default:
                    throw new ArgumentException("Unsupported native architecture: " + architecture, nameof(architecture));
            }
        }

        public static RuntimeAssemblyInfo Read(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var image = new PEReader(stream))
            {
                if (!image.HasMetadata || image.PEHeaders.CorHeader == null)
                {
                    return null;
                }

                var reader = image.GetMetadataReader();
                if (!reader.IsAssembly)
                {
                    return null;
                }

                var name = AssemblyName.GetAssemblyName(path);
                return new RuntimeAssemblyInfo
                {
                    Name = name.Name,
                    Identity = name.FullName,
                    Configuration = ReadConfiguration(reader),
                    Mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString(),
                    ReadyToRun = image.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0,
                    ILOnly = (image.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != 0,
                    MethodCount = reader.MethodDefinitions.Count,
                };
            }
        }

        public static int Verify(string originalPath, string compiledPath, string targetArchitecture = "x64")
        {
            var expectedMachine = ResolveMachine(targetArchitecture);
            using (var originalStream = File.OpenRead(originalPath))
            using (var compiledStream = File.OpenRead(compiledPath))
            using (var original = new PEReader(originalStream))
            using (var compiled = new PEReader(compiledStream))
            {
                var before = original.GetMetadataReader();
                var after = compiled.GetMetadataReader();
                if (compiled.PEHeaders.CorHeader == null ||
                    compiled.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size == 0 ||
                    compiled.PEHeaders.CoffHeader.Machine != expectedMachine ||
                    AssemblyName.GetAssemblyName(originalPath).FullName != AssemblyName.GetAssemblyName(compiledPath).FullName ||
                    before.GetGuid(before.GetModuleDefinition().Mvid) != after.GetGuid(after.GetModuleDefinition().Mvid))
                {
                    throw new InvalidDataException("ReadyToRun image or assembly identity differs: " + compiledPath);
                }

                if (before.MethodDefinitions.Count != after.MethodDefinitions.Count ||
                    before.CustomAttributes.Count != after.CustomAttributes.Count)
                {
                    throw new InvalidDataException("Managed metadata counts changed: " + compiledPath);
                }

                foreach (var handle in before.CustomAttributes)
                {
                    var left = before.GetCustomAttribute(handle);
                    var right = after.GetCustomAttribute(handle);
                    if (MetadataTokens.GetToken(left.Constructor) != MetadataTokens.GetToken(right.Constructor) ||
                        MetadataTokens.GetToken(left.Parent) != MetadataTokens.GetToken(right.Parent) ||
                        !before.GetBlobBytes(left.Value).SequenceEqual(after.GetBlobBytes(right.Value)))
                    {
                        throw new InvalidDataException("Managed attributes changed: " + compiledPath);
                    }
                }

                foreach (var handle in before.MethodDefinitions)
                {
                    var left = before.GetMethodDefinition(handle);
                    var right = after.GetMethodDefinition(handle);
                    if (left.Attributes != right.Attributes || left.ImplAttributes != right.ImplAttributes ||
                        before.GetString(left.Name) != after.GetString(right.Name) ||
                        !before.GetBlobBytes(left.Signature).SequenceEqual(after.GetBlobBytes(right.Signature)) ||
                        (left.RelativeVirtualAddress == 0) != (right.RelativeVirtualAddress == 0))
                    {
                        throw new InvalidDataException("Managed method metadata changed: " + compiledPath);
                    }

                    if (left.RelativeVirtualAddress == 0)
                    {
                        continue;
                    }

                    var leftBody = original.GetMethodBody(left.RelativeVirtualAddress);
                    var rightBody = compiled.GetMethodBody(right.RelativeVirtualAddress);
                    if (!leftBody.GetILBytes().SequenceEqual(rightBody.GetILBytes()) ||
                        leftBody.MaxStack != rightBody.MaxStack ||
                        leftBody.LocalVariablesInitialized != rightBody.LocalVariablesInitialized ||
                        leftBody.LocalSignature != rightBody.LocalSignature ||
                        leftBody.ExceptionRegions.Length != rightBody.ExceptionRegions.Length)
                    {
                        throw new InvalidDataException("Managed IL or locals changed: " + compiledPath);
                    }

                    for (var index = 0; index < leftBody.ExceptionRegions.Length; index++)
                    {
                        var first = leftBody.ExceptionRegions[index];
                        var second = rightBody.ExceptionRegions[index];
                        if (first.Kind != second.Kind || first.TryOffset != second.TryOffset ||
                            first.TryLength != second.TryLength || first.HandlerOffset != second.HandlerOffset ||
                            first.HandlerLength != second.HandlerLength || first.FilterOffset != second.FilterOffset ||
                            first.CatchType != second.CatchType)
                        {
                            throw new InvalidDataException("Managed exception regions changed: " + compiledPath);
                        }
                    }
                }

                return before.MethodDefinitions.Count;
            }
        }

        private static string ReadConfiguration(MetadataReader reader)
        {
            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }

                var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (member.Parent.Kind != HandleKind.TypeReference ||
                    reader.GetString(reader.GetTypeReference((TypeReferenceHandle)member.Parent).Name) != "AssemblyConfigurationAttribute")
                {
                    continue;
                }

                var blob = reader.GetBlobReader(attribute.Value);
                if (blob.ReadUInt16() != 1)
                {
                    throw new BadImageFormatException("Invalid assembly configuration attribute.");
                }

                return blob.ReadSerializedString();
            }

            return string.Empty;
        }
    }
}
