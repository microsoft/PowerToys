namespace Wox.Core.i18n
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