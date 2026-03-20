// ImageSizeReorderHelper.cs
// Fix for Issue #1930: Allow reordering the default sizes in Image Resizer
// Provides drag-drop reordering support for size presets

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ImageResizer.Utilities
{
    /// <summary>
    /// Helper for reordering Image Resizer size presets.
    /// </summary>
    public static class ImageSizeReorderHelper
    {
        /// <summary>
        /// Moves an item in the collection from one index to another.
        /// </summary>
        public static void MoveItem<T>(ObservableCollection<T> collection, int fromIndex, int toIndex)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
            
            if (fromIndex < 0 || fromIndex >= collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            
            if (toIndex < 0 || toIndex >= collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }
            
            if (fromIndex == toIndex)
            {
                return;
            }
            
            var item = collection[fromIndex];
            collection.RemoveAt(fromIndex);
            collection.Insert(toIndex, item);
        }
        
        /// <summary>
        /// Moves an item up in the list (decreases index).
        /// </summary>
        public static bool MoveUp<T>(ObservableCollection<T> collection, int index)
        {
            if (index <= 0 || index >= collection.Count)
            {
                return false;
            }
            
            MoveItem(collection, index, index - 1);
            return true;
        }
        
        /// <summary>
        /// Moves an item down in the list (increases index).
        /// </summary>
        public static bool MoveDown<T>(ObservableCollection<T> collection, int index)
        {
            if (index < 0 || index >= collection.Count - 1)
            {
                return false;
            }
            
            MoveItem(collection, index, index + 1);
            return true;
        }
    }
}
