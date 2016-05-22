using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Wox.Infrastructure.Image
{
    [Serializable]
    public class ImageCache
    {
        private const int MaxCached = 200;
        public ConcurrentDictionary<string, int> TopUsedImages = new ConcurrentDictionary<string, int>();

        public void Add(string path)
        {
            if (TopUsedImages.ContainsKey(path))
            {
                TopUsedImages[path] = TopUsedImages[path] + 1;
            }
            else
            {
                TopUsedImages[path] = 1;
            }
        }

        public void Cleanup()
        {
            if (TopUsedImages.Count > MaxCached)
            {
                var images = TopUsedImages.OrderByDescending(o => o.Value)
                                          .Take(MaxCached)
                                          .ToDictionary(i => i.Key, i => i.Value);
                TopUsedImages = new ConcurrentDictionary<string, int>(images);
            }
        }
    }
}
