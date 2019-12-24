using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace ImageResizer.Test
{
    static class AssertEx
    {
        public static void All<T>(IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection)
            {
                action(item);
            }
        }

        public static void Image(string path, Action<BitmapDecoder> action)
        {
            using (var stream = File.OpenRead(path))
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

            Assert.NotNull(raisedEvent);

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

            Assert.NotNull(raisedEvent);

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
