using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.ApplicationModel.Chat;

namespace Wox.Infrastructure.Image
{
    [Serializable]
    public class ImageCache
    {
        private const int MaxCached = 50;
        public ConcurrentDictionary<string, int> Usage = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, ImageSource> _data = new ConcurrentDictionary<string, ImageSource>();
        private int cnt = 0;

        public ImageSource this[string path]
        {
            get
            {
                Usage.AddOrUpdate(path, 1, (k, v) => v + 1);
                var i = _data[path];
                return i;
            }
            set 
            {
                _data[path] = value;

                if (_data.Count > 2 * MaxCached)
                {
                    Task.Run(() =>
                    {
                        Cleanup();
                        foreach (var key in _data.Keys)
                        {
                            int dictValue;
                            if (!Usage.TryGetValue(key, out dictValue))
                            {
                                ImageSource test;
                                _data.TryRemove(key, out test);
                            }
                        }
                    }
                    );
                }
            }
        }

        public void Cleanup()
        {
            var images = Usage
                .OrderByDescending(o => o.Value)
                .Take(MaxCached)
                .ToDictionary(i => i.Key, i => i.Value);
            Usage = new ConcurrentDictionary<string, int>(images);
        }

        public bool ContainsKey(string key)
        {
            var contains = _data.ContainsKey(key);
            return contains;
        }

        public int CacheSize()
        {
            return _data.Count;
        }

        /// <summary>
        /// return the number of unique images in the cache (by reference not by checking images content)
        /// </summary>
        public int UniqueImagesInCache()
        {
            return _data.Values.Distinct().Count();
        }
    }

}