// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1636:FileHeaderCopyrightTextMustMatch", Justification = "File created under PowerToys.")]

namespace ImageResizer.Test
{
    internal static class AssertEx
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();

        public static void All<T>(IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection)
            {
                action(item);
            }
        }

        public static void Image(string path, Action<BitmapDecoder> action)
        {
            using (var stream = _fileSystem.File.OpenRead(path))
            {
                var image = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);

                action(image);
            }
        }

        public static RaisedEvent<NotifyCollectionChangedEventArgs> Raises<T>(
            Action<NotifyCollectionChangedEventHandler> attach,
            Action<NotifyCollectionChangedEventHandler> detach,
            Action testCode)
            where T : NotifyCollectionChangedEventArgs
        {
            RaisedEvent<NotifyCollectionChangedEventArgs> raisedEvent = null;
            NotifyCollectionChangedEventHandler handler = (sender, e)
                => raisedEvent = new RaisedEvent<NotifyCollectionChangedEventArgs>(sender, e);
            attach(handler);
            testCode();
            detach(handler);

            Assert.IsNotNull(raisedEvent);

            return raisedEvent;
        }

        public static RaisedEvent<PropertyChangedEventArgs> Raises<T>(
            Action<PropertyChangedEventHandler> attach,
            Action<PropertyChangedEventHandler> detach,
            Action testCode)
            where T : PropertyChangedEventArgs
        {
            RaisedEvent<PropertyChangedEventArgs> raisedEvent = null;
            PropertyChangedEventHandler handler = (sender, e)
                => raisedEvent = new RaisedEvent<PropertyChangedEventArgs>(sender, e);
            attach(handler);
            testCode();
            detach(handler);

            Assert.IsNotNull(raisedEvent);

            return raisedEvent;
        }

        public class RaisedEvent<TArgs>
        {
            public RaisedEvent(object sender, TArgs args)
            {
                Sender = sender;
                Arguments = args;
            }

            public object Sender { get; }

            public TArgs Arguments { get; }
        }
    }
}
