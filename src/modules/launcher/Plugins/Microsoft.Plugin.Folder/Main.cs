// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder
{
    public class Main : IPlugin, IPluginI18n, ISavable, IContextMenu, IDisposable
    {
        public const string FolderImagePath = "Images\\folder.dark.png";
        public const string FileImagePath = "Images\\file.dark.png";
        public const string DeleteFileFolderImagePath = "Images\\delete.dark.png";
        public const string CopyImagePath = "Images\\copy.dark.png";

        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly PluginJsonStorage<FolderSettings> _storage = new PluginJsonStorage<FolderSettings>();
        private static readonly FolderSettings _settings = _storage.Load();
        private static readonly IQueryInternalDirectory _internalDirectory = new QueryInternalDirectory(_settings, new QueryFileSystemInfo(_fileSystem.DirectoryInfo), _fileSystem.Directory);
        private static readonly FolderHelper _folderHelper = new FolderHelper(new DriveInformation(), new FolderLinksSettings(_settings));

        private static readonly ICollection<IFolderProcessor> _processors = new IFolderProcessor[]
        {
            new UserFolderProcessor(_folderHelper),
            new InternalDirectoryProcessor(_folderHelper, _internalDirectory),
        };

        private static PluginInitContext _context;
        private IContextMenu _contextMenuLoader;
        private bool _disposed;

        public string Name => Properties.Resources.wox_plugin_folder_plugin_name;

        public string Description => Properties.Resources.wox_plugin_folder_plugin_description;

        public void Save()
        {
            _storage.Save();
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var expandedName = FolderHelper.Expand(query.Search);

            return _processors.SelectMany(processor => processor.Results(query.ActionKeyword, expandedName))
                .Select(res => res.Create(_context.API))
                .Select(AddScore)
                .ToList();
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _contextMenuLoader = new ContextMenuLoader(context);

            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public static IEnumerable<Result> GetFolderPluginResults(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var expandedName = FolderHelper.Expand(query.Search);

            return _processors.SelectMany(processor => processor.Results(query.ActionKeyword, expandedName))
                .Select(res => res.Create(_context.API))
                .Select(AddScore);
        }

        private static void UpdateIconPath(Theme theme)
        {
            QueryInternalDirectory.SetWarningIcon(theme);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "The parameter is unused")]
        private static void OnThemeChanged(Theme _, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        // todo why was this hack here?
        private static Result AddScore(Result result)
        {
            result.Score += 10;
            return result;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.wox_plugin_folder_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.wox_plugin_folder_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
