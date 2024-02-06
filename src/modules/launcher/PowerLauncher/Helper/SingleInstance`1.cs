// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instance restart
namespace PowerLauncher.Helper
{
    /// <summary>
    /// This class checks to make sure that only one instance of
    /// this application is running at a time.
    /// </summary>
    /// <remarks>
    /// Note: this class should be used with some caution, because it does no
    /// security checking. For example, if one instance of an app that uses this class
    /// is running as Administrator, any other instance, even if it is not
    /// running as Administrator, can activate it with command line arguments.
    /// For most apps, this will not be much of an issue.
    /// </remarks>
    public static class SingleInstance<TApplication>
                where TApplication : Application, ISingleInstanceApp
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        /// <summary>
        /// String delimiter used in channel names.
        /// </summary>
        private const string Delimiter = ":";

        /// <summary>
        /// Suffix to the channel name.
        /// </summary>
        private const string ChannelNameSuffix = "SingeInstanceIPCChannel";
        private const string InstanceMutexName = @"Local\PowerToys_Run_InstanceMutex";

        /// <summary>
        /// Gets or sets application mutex.
        /// </summary>
        internal static Mutex SingleInstanceMutex { get; set; }

        internal static void CreateInstanceMutex()
        {
            SingleInstanceMutex = new Mutex(true, InstanceMutexName, out bool firstInstance);
        }

        /// <summary>
        /// Checks if the instance of the application attempting to start is the first instance.
        /// If not, activates the first instance.
        /// </summary>
        /// <returns>True if this is the first instance of the application.</returns>
        internal static bool InitializeAsFirstInstance()
        {
            // Build unique application Id and the IPC channel name.
            string applicationIdentifier = InstanceMutexName + Environment.UserName;

            string channelName = string.Concat(applicationIdentifier, Delimiter, ChannelNameSuffix);

            SingleInstanceMutex = new Mutex(true, InstanceMutexName, out bool firstInstance);
            if (firstInstance)
            {
                _ = CreateRemoteService(channelName);
                return true;
            }
            else
            {
                _ = SignalFirstInstance(channelName);
                return false;
            }
        }

        /// <summary>
        /// Cleans up single-instance code, clearing shared resources, mutexes, etc.
        /// </summary>
        internal static void Cleanup()
        {
            SingleInstanceMutex?.ReleaseMutex();
        }

        /// <summary>
        /// Gets command line args - for ClickOnce deployed applications, command line args may not be passed directly, they have to be retrieved.
        /// </summary>
        /// <returns>List of command line arg strings.</returns>
        private static List<string> GetCommandLineArgs(string uniqueApplicationName)
        {
            string[] args = null;

            try
            {
                // The application was not clickonce deployed, get args from standard API's
                args = Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                // The application was clickonce deployed
                // Clickonce deployed apps cannot receive traditional commandline arguments
                // As a workaround commandline arguments can be written to a shared location before
                // the app is launched and the app can obtain its commandline arguments from the
                // shared location
                string appFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), uniqueApplicationName);

                string cmdLinePath = Path.Combine(appFolderPath, "cmdline.txt");
                if (File.Exists(cmdLinePath))
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(cmdLinePath, Encoding.Unicode))
                        {
                            args = NativeMethods.CommandLineToArgvW(reader.ReadToEnd());
                        }

                        File.Delete(cmdLinePath);
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            if (args == null)
            {
                args = Array.Empty<string>();
            }

            return new List<string>(args);
        }

        /// <summary>
        /// Creates a remote server pipe for communication.
        /// Once receives signal from client, will activate first instance.
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        private static async Task CreateRemoteService(string channelName)
        {
            using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(channelName, PipeDirection.In))
            {
                while (true)
                {
                    // Wait for connection to the pipe
                    await pipeServer.WaitForConnectionAsync().ConfigureAwait(false);
                    if (Application.Current != null)
                    {
                        // Do an asynchronous call to ActivateFirstInstance function
                        Application.Current.Dispatcher.Invoke(ActivateFirstInstance);
                    }

                    // Disconnect client
                    pipeServer.Disconnect();
                }
            }
        }

        /// <summary>
        /// Creates a client pipe and sends a signal to server to launch first instance
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        /// Command line arguments for the second instance, passed to the first instance to take appropriate action.
        /// </param>
        private static async Task SignalFirstInstance(string channelName)
        {
            // Create a client pipe connected to server
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", channelName, PipeDirection.Out))
            {
                // Connect to the available pipe
                await pipeClient.ConnectAsync(0).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Activates the first instance of the application with arguments from a second instance.
        /// </summary>
        /// <param name="args">List of arguments to supply the first instance of the application.</param>
        private static void ActivateFirstInstance()
        {
            // Set main window state and process command line args
            if (Application.Current == null)
            {
                return;
            }

            ((TApplication)Application.Current).OnSecondAppStarted();
        }
    }

    public interface ISingleInstanceApp
    {
        void OnSecondAppStarted();
    }
}
