using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Core.Theme
{
    public class ThemeManager
    {
        private static Theme instance;
        private static object syncObject = new object();

        public static Theme Theme
        {
            get
            {
                if (instance == null)
                {
                    lock (syncObject)
                    {
                        if (instance == null)
                        {
                            instance = new Theme();
                        }
                    }
                }
                return instance;
            }
        }
    }
}
