using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
