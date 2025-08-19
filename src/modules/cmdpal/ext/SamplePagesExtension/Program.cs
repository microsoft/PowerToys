// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using WinRT;

namespace SamplePagesExtension;

public class Program
{
    private static ManualResetEvent _comServerEventHandle = new(false);

    [MTAThread]
    [DynamicWindowsRuntimeCast(typeof(IExtension))]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();

            // ManualResetEvent extensionDisposedEvent = new(false);

            //// We are instantiating an extension instance once above, and returning it every time the callback in RegisterExtension below is called.
            //// This makes sure that only one instance of SampleExtension is alive, which is returned every time the host asks for the IExtension object.
            //// If you want to instantiate a new instance each time the host asks, create the new instance inside the delegate.
            // SampleExtension extensionInstance = new(extensionDisposedEvent);
            // server.RegisterClass<SampleExtension, IExtension>(() => extensionInstance);
            RegisterExtension<SampleExtension, IExtension>(
                server,
                (EventWaitHandle handle) =>
                {
                    SampleExtension extension = new();

                    extension.Disposed += (s, e) =>
                    {
                        handle.Set();
                    };
                    return extension;
                });

            server.Start();

            // This will make the main thread wait until the event is signalled by the extension class.
            // Since we have a single instance of the extension object, we exit as soon as it is disposed.
            _comServerEventHandle.WaitOne();
        }
        else
        {
            Console.WriteLine("Not being launched as a Extension... exiting.");
        }
    }

    [DynamicWindowsRuntimeCast(typeof(IExtension))]
    public static void RegisterExtension<TExtension, TInterface>(
        ComServer server,
        Func<EventWaitHandle, TExtension> factory)
        where TExtension : class, TInterface, new()
    {
        TExtension extension = null;

        _ = Task.Run(() =>
        {
            _comServerEventHandle.WaitOne();
            extension = null;
        });

        server.RegisterClass<TExtension, TInterface>(() =>
        {
            if (extension == null)
            {
                extension = factory(_comServerEventHandle);
            }

            return extension!;
        });

        // Shmuelie.WinRTServer.CsWinRT.ComServerExtensions.RegisterClass<TExtension, TInterface>(
        //    server,
        //    () =>
        //    {
        //        if (extension == null)
        //        {
        //            extension = factory(_comServerEventHandle);
        //        }

        // return extension!;
        //    });
    }
}
