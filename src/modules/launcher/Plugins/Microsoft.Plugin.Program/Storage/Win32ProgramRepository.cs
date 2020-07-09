using System;
using System.Collections.Generic;
using Microsoft.Plugin.Program.Programs;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramRepository : ListRepository<Programs.Win32>, IProgramRepository
    {
        private IStorage<IList<Programs.Win32>> _storage;
        private Settings _settings;

        public Win32ProgramRepository(IStorage<IList<Programs.Win32>> storage, Settings settings)
        {
            this._storage = storage ?? throw new ArgumentNullException("storage", "Win32ProgramRepository requires an initialized storage interface");
            this._settings = settings ?? throw new ArgumentNullException("settings", "Win32ProgramRepository requires an initialized settings object");
        }

        public void IndexPrograms()
        {
            var applications = Programs.Win32.All(_settings);
            Set(applications);
        }

        public void Save()
        {
            _storage.Save(Items);
        }

        public void Load()
        {
            var items = _storage.TryLoad(new Programs.Win32[] { });
            Set(items);
        }
    }
}
