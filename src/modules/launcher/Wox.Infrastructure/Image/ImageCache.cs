// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Image
{
    [Serializable]
    public class ImageCache
    {
        private const int MaxCached = 50;
        private const int PermissibleFactor = 2;

        private readonly ConcurrentDictionary<string, ImageSource> _data = new ConcurrentDictionary<string, ImageSource>();
        private readonly WoxJsonStorage<ConcurrentDictionary<string, int>> _storage;

        public ConcurrentDictionary<string, int> Usage { get; private set; } = new ConcurrentDictionary<string, int>();

        public ImageCache()
        {
            _storage = new WoxJsonStorage<ConcurrentDictionary<string, int>>("ImageCache");
            Usage = _storage.Load();
        }

        public ImageSource this[string path]
        {
            get
            {
                Usage.AddOrUpdate(path, 1, (k, v) => v + 1);
                _data.TryGetValue(path, out ImageSource i);

                if (i == null)
                {
                    Log.Warn($"ImageSource is null for path: {path}", GetType());
                }

                return i;
            }

            set
            {
                _data[path] = value;

                // To prevent the dictionary from drastically increasing in size by caching images, the dictionary size is not allowed to grow more than the permissibleFactor * maxCached size
                // This is done so that we don't constantly perform this resizing operation and also maintain the image cache size at the same time
                if (_data.Count > PermissibleFactor * MaxCached)
                {
                    // This function resizes the Usage dictionary, taking the top 'maxCached' number of items and filtering the image icons that are not accessed frequently.
                    Cleanup();

                    // To delete the images from the data dictionary based on the resizing of the Usage Dictionary.
                    foreach (var key in _data.Keys)
                    {
                        // Using Ordinal since this is internal
                        if (!Usage.TryGetValue(key, out _) && !(key.Equals(Constant.ErrorIcon, StringComparison.Ordinal) || key.Equals(Constant.LightThemedErrorIcon, StringComparison.Ordinal)))
                        {
                            _data.TryRemove(key, out _);
                        }
                    }
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

        public Dictionary<string, int> GetUsageAsDictionary()
        {
            return new Dictionary<string, int>(Usage);
        }

        public void Save()
        {
            _storage.Save();
        }
    }
}
