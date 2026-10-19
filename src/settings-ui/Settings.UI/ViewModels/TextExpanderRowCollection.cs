// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A list of rows that is brought in line with a new list in one step. One row added, changed
    /// or removed, as after a save, is one notification, so the rows around it stay as they are.
    /// Anything bigger, such as a search or a reload, is a single reset rather than a notification
    /// for every row.
    /// </summary>
    /// <remarks>Rows are matched by reference.</remarks>
    public sealed class TextExpanderRowCollection : ObservableCollection<TextExpanderSnippetViewModel>
    {
        /// <summary>Makes this list hold <paramref name="target"/>, in its order.</summary>
        public void SyncTo(IReadOnlyList<TextExpanderSnippetViewModel> target)
        {
            int mismatch = FirstMismatch(target);
            if (mismatch == Count && Count == target.Count)
            {
                return;
            }

            if (Count == target.Count && SameAfter(target, mismatch + 1, mismatch + 1))
            {
                this[mismatch] = target[mismatch];
            }
            else if (Count + 1 == target.Count && SameAfter(target, mismatch + 1, mismatch))
            {
                Insert(mismatch, target[mismatch]);
            }
            else if (Count == target.Count + 1 && SameAfter(target, mismatch, mismatch + 1))
            {
                RemoveAt(mismatch);
            }
            else
            {
                ReplaceAll(target);
            }
        }

        /// <summary>Replaces every row with a single reset notification.</summary>
        public void ReplaceAll(IEnumerable<TextExpanderSnippetViewModel> rows)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (TextExpanderSnippetViewModel row in rows)
            {
                Items.Add(row);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private int FirstMismatch(IReadOnlyList<TextExpanderSnippetViewModel> target)
        {
            int limit = System.Math.Min(Count, target.Count);
            int index = 0;
            while (index < limit && ReferenceEquals(this[index], target[index]))
            {
                index++;
            }

            return index;
        }

        /// <summary>Gets a value indicating whether the rest of this list, from <paramref name="ownIndex"/>, is the rest of <paramref name="target"/>, from <paramref name="targetIndex"/>.</summary>
        private bool SameAfter(IReadOnlyList<TextExpanderSnippetViewModel> target, int targetIndex, int ownIndex)
        {
            if (Count - ownIndex != target.Count - targetIndex)
            {
                return false;
            }

            for (; ownIndex < Count; ownIndex++, targetIndex++)
            {
                if (!ReferenceEquals(this[ownIndex], target[targetIndex]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
