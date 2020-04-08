// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System.Threading;
using System.Windows;

namespace WindowWalker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex _appMutex;

        public App()
        {
            // Check if there is already a running instance
            _appMutex = new Mutex(false, Guid);
            if (!_appMutex.WaitOne(0, false))
            {
                Shutdown();
            }
        }

        private static readonly string Guid = "5dedb2a2-690f-45db-9ef7-07605223a70a";
    }
}
