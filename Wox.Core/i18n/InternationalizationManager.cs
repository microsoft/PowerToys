using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Wox.Core.UI;
using Wox.Infrastructure.Logger;

namespace Wox.Core.i18n
{
    public static class InternationalizationManager
    {
        private static Internationalization instance;
        private static object syncObject = new object();

        public static Internationalization Internationalization
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