using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Wox.Infrastructure.Image
{
    [Serializable]
    public class ImageCache
    {
        private const int MaxCached = 5000;
        public ConcurrentDictionary<string, int> Usage = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, ImageSource> _data = new ConcurrentDictionary<string, ImageSource>();


        public ImageSource this[string path]
        {
            get
            {
                Usage.AddOrUpdate(path, 1, (k, v) => v + 1);
                var i = _data[path];
                return i;
            }
            set { _data[path] = value; }
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
    }

}
