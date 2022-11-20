// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PeekUI.Extensions
{
    public static class LinkedListNodeExtensions
    {
        public static LinkedListNode<T>? GetNextOrFirst<T>(this LinkedListNode<T> current)
        {
            return current.Next ?? current.List?.First;
        }

        public static LinkedListNode<T>? GetPreviousOrLast<T>(this LinkedListNode<T> current)
        {
            return current.Previous ?? current.List?.Last;
        }
    }
}
