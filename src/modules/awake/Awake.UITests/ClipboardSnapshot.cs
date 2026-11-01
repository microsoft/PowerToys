// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.Awake.UITests;

internal sealed class ClipboardSnapshot : IDisposable
{
    private readonly List<IDisposable> resources = [];
    private readonly Forms.DataObject? contents;
    private bool disposed;

    internal ClipboardSnapshot()
    {
        try
        {
            contents = OnSta(() =>
            {
                var source = Forms.Clipboard.GetDataObject();
                if (source is null)
                {
                    return null;
                }

                var copy = new Forms.DataObject();
                foreach (var format in source.GetFormats(autoConvert: false))
                {
                    var data = source.GetData(format, autoConvert: format == Forms.DataFormats.Bitmap)
                        ?? throw new InvalidDataException($"Clipboard format '{format}' could not be captured.");
                    if (data is Bitmap bitmap)
                    {
                        var clone = (Bitmap)bitmap.Clone();
                        resources.Add(clone);
                        data = clone;
                    }
                    else if (data is Stream stream)
                    {
                        var clone = new MemoryStream();
                        var position = stream.CanSeek ? stream.Position : 0;
                        if (stream.CanSeek)
                        {
                            stream.Position = 0;
                        }

                        try
                        {
                            stream.CopyTo(clone);
                        }
                        finally
                        {
                            if (stream.CanSeek)
                            {
                                stream.Position = position;
                            }
                        }

                        clone.Position = 0;
                        resources.Add(clone);
                        data = clone;
                    }

                    copy.SetData(format, autoConvert: false, data);
                }

                return copy;
            });
        }
        catch
        {
            foreach (var resource in resources)
            {
                resource.Dispose();
            }

            throw;
        }
    }

    internal static T OnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        result = action();
                        break;
                    }
                    catch (ExternalException exception) when (exception.ErrorCode == unchecked((int)0x800401D0) && attempt < 10)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The clipboard operation did not finish.");
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            OnSta(() =>
            {
                if (contents is null)
                {
                    Forms.Clipboard.Clear();
                }
                else
                {
                    Forms.Clipboard.SetDataObject(contents, copy: true, retryTimes: 10, retryDelay: 100);
                }

                return true;
            });
        }
        finally
        {
            foreach (var resource in resources)
            {
                resource.Dispose();
            }
        }
    }
}
