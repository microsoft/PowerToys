namespace Wox.Core.Resource
{
    public class ThemeManager
    {
        private static Theme instance;
        private static object syncObject = new object();

        public static Theme Instance
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
