using Windows.ApplicationModel;

namespace Microsoft.Plugin.Program.Storage
{
    internal interface IProgramRepository
    {
        void IndexPrograms();
        void Load();
        void Save();
    }
}