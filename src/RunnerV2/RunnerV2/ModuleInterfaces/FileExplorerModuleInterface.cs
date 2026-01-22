// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class FileExplorerModuleInterface : IPowerToysModule, IPowerToysModuleSettingsChangedSubscriber
    {
        private record struct FileExplorerModule(Func<bool> IsEnabled, GpoRuleConfigured GpoRule, RegistryChangeSet RegistryChanges);

        private static readonly List<FileExplorerModule> _fileExplorerModules;

        private static readonly string[] ExtSVG = { ".svg" };
        private static readonly string[] ExtMarkdown = { ".md", ".markdown", ".mdown", ".mkdn", ".mkd", ".mdwn", ".mdtxt", ".mdtext" };
        private static readonly string[] ExtPDF = { ".pdf" };
        private static readonly string[] ExtGCode = { ".gcode" };
        private static readonly string[] ExtBGCode = { ".bgcode" };
        private static readonly string[] ExtSTL = { ".stl" };
        private static readonly string[] ExtQOI = { ".qoi" };

        static FileExplorerModuleInterface()
        {
            static PowerPreviewProperties GetProperties() => SettingsUtils.Default.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties;

            string installationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _fileExplorerModules = [
                new FileExplorerModule(
                    () => GetProperties().EnableBgcodePreview,
                    GPOWrapper.GetConfiguredBgcodePreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{0e6d5bdd-d5f8-4692-a089-8bb88cdd37f4}", "BgcodePreviewHandler", "Binary G-code Preview Handler", ExtBGCode)),
                new FileExplorerModule(
                    () => GetProperties().EnableBgcodeThumbnail,
                    GPOWrapper.GetConfiguredBgcodeThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{5c93a1e4-99d0-4fb3-991c-6c296a27be21}", "BgcodeThumbnailProvider", "Binary G-code Thumbnail Provider", ExtBGCode)),
                new FileExplorerModule(
                    () => GetProperties().EnableGcodePreview,
                    GPOWrapper.GetConfiguredGcodePreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{A0257634-8812-4CE8-AF11-FA69ACAEAFAE}", "GcodePreviewHandler", "G-code Preview Handler", ExtGCode)),
                new FileExplorerModule(
                    () => GetProperties().EnableGcodeThumbnail,
                    GPOWrapper.GetConfiguredGcodeThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{F2847CBE-CD03-4C83-A359-1A8052C1B9D5}", "GcodeThumbnailProvider", "G-code Thumbnail Provider", ExtGCode)),
                new FileExplorerModule(
                    () => GetProperties().EnableMdPreview,
                    GPOWrapper.GetConfiguredMarkdownPreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{60789D87-9C3C-44AF-B18C-3DE2C2820ED3}", "MarkdownPreviewHandler", "Markdown Preview Handler", ExtMarkdown)),
                new FileExplorerModule(
                    () => GetProperties().EnablePdfPreview,
                    GPOWrapper.GetConfiguredPdfPreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{A5A41CC7-02CB-41D4-8C9B-9087040D6098}", "PdfPreviewHandler", "PDF Preview Handler", ExtPDF)),
                new FileExplorerModule(
                    () => GetProperties().EnablePdfThumbnail,
                    GPOWrapper.GetConfiguredPdfThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{D8BB9942-93BD-412D-87E4-33FAB214DC1A}", "PdfThumbnailProvider", "PDF Thumbnail Provider", ExtPDF)),
                new FileExplorerModule(
                    () => GetProperties().EnableQoiPreview,
                    GPOWrapper.GetConfiguredQoiPreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{729B72CD-B72E-4FE9-BCBF-E954B33FE699}", "QoiPreviewHandler", "QOI Preview Handler", ExtQOI)),
                new FileExplorerModule(
                    () => GetProperties().EnableQoiThumbnail,
                    GPOWrapper.GetConfiguredQoiThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{AD856B15-D25E-4008-AFB7-AFAA55586188}", "QoiThumbnailProvider", "QOI Thumbnail Provider", ExtQOI, "image", "Picture")),
                new FileExplorerModule(
                    () => GetProperties().EnableStlThumbnail,
                    GPOWrapper.GetConfiguredStlThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{77257004-6F25-4521-B602-50ECC6EC62A6}", "StlThumbnailProvider", "STL Thumbnail Provider", ExtSTL)),
                new FileExplorerModule(
                    () => GetProperties().EnableSvgPreview,
                    GPOWrapper.GetConfiguredSvgPreviewEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.PreviewHandler, "{FCDD4EED-41AA-492F-8A84-31A1546226E0}", "SvgPreviewHandler", "SVG Preview Handler", ExtSVG)),
                new FileExplorerModule(
                    () => GetProperties().EnableSvgThumbnail,
                    GPOWrapper.GetConfiguredSvgThumbnailsEnabledValue(),
                    GetFileExplorerAddOnChangeSet(FileExplorerAddOnType.ThumbnailProvider, "{10144713-1526-46C9-88DA-1FB52807A9FF}", "SvgThumbnailProvider", "SVG Thumbnail Provider", ExtSVG, "image", "Picture")),
                GetMonacoFileExplorerModule(installationPath)
            ];
        }

        private static FileExplorerModule GetMonacoFileExplorerModule(string installationPath)
        {
            // .svgz is a binary file type that Monaco cannot handle, so we exclude it from the preview handler
            string[] extExclusions = [..ExtMarkdown, ..ExtSVG, ".svgz"];
            List<string> extensions = [];

            string languagesFilePath = Path.Combine(installationPath, "Assets\\Monaco\\monaco_languages.json");

            if (!File.Exists(languagesFilePath))
            {
                Logger.LogError("PowerPreviewModuleInterface: Unable to find monaco_languages.json file at " + languagesFilePath);
                goto returnLabel;
            }

            try
            {
                JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(languagesFilePath));
                var list = jsonDocument.RootElement.GetProperty("list");

                foreach (var item in list.EnumerateArray())
                {
                    if (item.TryGetProperty("extensions", out JsonElement extensionsElement))
                    {
                        foreach (var ext in extensionsElement.EnumerateArray())
                        {
                            string extension = ext.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(extension) && !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                            {
                                extensions.Add(extension);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("PowerPreviewModuleInterface: Failed to parse monaco_languages.json file.", ex);
            }

        returnLabel:
            return new FileExplorerModule(
                () => SettingsUtils.Default.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.EnableMonacoPreview,
                GPOWrapper.GetConfiguredMonacoPreviewEnabledValue(),
                GetFileExplorerAddOnChangeSet(
                    FileExplorerAddOnType.PreviewHandler,
                    "{D8034CFA-F34B-41FE-AD45-62FCBB52A6DA}",
                    "MonacoPreviewHandler",
                    "Monaco Preview Handler",
                    [.. extensions.Where(ext => !extExclusions.Contains(ext))]));
        }

        private enum FileExplorerAddOnType
        {
            ThumbnailProvider,
            PreviewHandler,
        }

        private static RegistryChangeSet GetFileExplorerAddOnChangeSet(FileExplorerAddOnType type, string handlerClsid, string className, string displayName, string[] fileTypes, string? perceivedType = null, string? fileKindType = null)
        {
            string installationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string pathToHandler = Path.Combine(installationPath, "PowerToys.FileExplorerDllExporter.dll");
            string clsidPath = "Software\\Classes\\CLSID\\" + handlerClsid;
            string inprocServer32Path = clsidPath + "\\InprocServer32";

            string assemblyKeyValue;
            int lastDotPos = className.LastIndexOf('.');

            if (lastDotPos != -1)
            {
                assemblyKeyValue = string.Concat("PowerToys.", className.AsSpan(lastDotPos + 1));
            }
            else
            {
                assemblyKeyValue = "PowerToys." + className;
            }

            assemblyKeyValue += $", Version={Assembly.GetExecutingAssembly().GetName().Version!}, Culture=neutral";

            List<RegistryValueChange> changes = [
                new RegistryValueChange
                {
                    KeyPath = clsidPath,
                    KeyName = "DisplayName",
                    Value = displayName,
                },
                new RegistryValueChange
                {
                    KeyPath = clsidPath,
                    KeyName = null,
                    Value = className,
                },
                new RegistryValueChange
                {
                    KeyPath = inprocServer32Path,
                    KeyName = null,
                    Value = pathToHandler,
                },
                new RegistryValueChange
                {
                    KeyPath = inprocServer32Path,
                    KeyName = "Assembly",
                    Value = assemblyKeyValue,
                },
                new RegistryValueChange
                {
                    KeyPath = inprocServer32Path,
                    KeyName = "Class",
                    Value = className,
                },
                new RegistryValueChange
                {
                    KeyPath = inprocServer32Path,
                    KeyName = "ThreadingModel",
                    Value = "Apartment",
                },
            ];

            foreach (string fileType in fileTypes)
            {
                string fileTypePath = "Software\\Classes\\" + fileType;
                string fileAssociationPath = fileTypePath + "\\shellex\\" + (type == FileExplorerAddOnType.PreviewHandler ? IPREVIEWHANDLERCLSID : ITHUMBNAILPROVIDERCLSID);

                changes.Add(new RegistryValueChange
                {
                    KeyPath = fileAssociationPath,
                    KeyName = null,
                    Value = handlerClsid,
                });

                if (!string.IsNullOrEmpty(fileKindType))
                {
                    // Registering a file type as a kind needs to be done at the HKEY_LOCAL_MACHINE level.
                    // Make it optional as well so that we don't fail registering the handler if we can't write to HKEY_LOCAL_MACHINE.
                    string kindPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\KindMap";
                    changes.Add(new RegistryValueChange
                    {
                        Scope = Microsoft.Win32.RegistryHive.LocalMachine,
                        KeyPath = kindPath,
                        KeyName = fileType,
                        Value = fileKindType,
                        Required = false,
                    });
                }

                if (!string.IsNullOrEmpty(perceivedType))
                {
                    changes.Add(new RegistryValueChange
                    {
                        KeyPath = fileTypePath,
                        KeyName = "PerceivedType",
                        Value = perceivedType,
                    });
                }

                // this regfile registry key has precedence over Software\Classes\.reg for .reg files
                if (type == FileExplorerAddOnType.PreviewHandler && fileType.Equals(".reg", StringComparison.OrdinalIgnoreCase))
                {
                    string regFilePath = "Software\\Classes\\regfile\\shellex\\" + IPREVIEWHANDLERCLSID;
                    changes.Add(new RegistryValueChange
                    {
                        KeyPath = regFilePath,
                        KeyName = null,
                        Value = handlerClsid,
                    });
                }
            }

            if (type == FileExplorerAddOnType.PreviewHandler)
            {
                string previewHostClsid = "{6d2b5079-2f0b-48dd-ab7f-97cec514d30b}";
                string previewHandlerListPath = "(Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers)";
                changes.Add(new RegistryValueChange
                {
                    KeyPath = clsidPath,
                    KeyName = "AppID",
                    Value = previewHostClsid,
                });

                changes.Add(new RegistryValueChange
                {
                    KeyPath = previewHandlerListPath,
                    KeyName = handlerClsid,
                    Value = displayName,
                });
            }

            changes.Add(new RegistryValueChange
            {
                Scope = Microsoft.Win32.RegistryHive.LocalMachine,
                KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved",
                KeyName = handlerClsid,
                Value = displayName,
                Required = false,
            });

            return new RegistryChangeSet
            {
                Changes = [.. changes],
            };
        }

        private const string ITHUMBNAILPROVIDERCLSID = "{E357FCCD-A995-4576-B01F-234630154E96}";
        private const string IPREVIEWHANDLERCLSID = "{8895b1c6-b41f-4c1c-a562-0d564250836f}";

        public string Name => "File Explorer";

        public bool Enabled => true;

        public GpoRuleConfigured GpoRuleConfigured => GpoRuleConfigured.Unavailable;

        public void Disable()
        {
        }

        public void Enable()
        {
            OnSettingsChanged();
        }

        public void OnSettingsChanged()
        {
            foreach (FileExplorerModule submodule in _fileExplorerModules)
            {
                if (submodule.GpoRule == GpoRuleConfigured.Disabled)
                {
                    submodule.RegistryChanges.UnApply();
                    continue;
                }

                if (submodule.IsEnabled() || submodule.GpoRule == GpoRuleConfigured.Enabled)
                {
                    submodule.RegistryChanges.Apply();
                }
                else
                {
                    submodule.RegistryChanges.UnApply();
                }
            }
        }
    }
}
