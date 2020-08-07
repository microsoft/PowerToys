// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Core.Resource
{
    public static class InternationalizationManager
    {
        private static Internationalization instance;
        private static object syncObject = new object();

        public static Internationalization Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncObject)
                    {
                        if (instance == null)
                        {
                            instance = new Internationalization();
                        }
                    }
                }

                return instance;
            }
        }
    }
}
