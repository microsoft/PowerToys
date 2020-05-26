namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Save plugin settings/cache, 
    /// todo should be merged into a abstract class instead of separate interface
    /// </summary>
    public interface ISavable
    {
        void Save();
    }
}
