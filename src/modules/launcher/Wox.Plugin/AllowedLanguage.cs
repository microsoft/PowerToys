namespace Wox.Plugin
{
    public static class AllowedLanguage
    {
        public static string CSharp
        {
            get { return "CSHARP"; }
        }

        public static string Executable
        {
            get { return "EXECUTABLE"; }
        }

        public static bool IsAllowed(string language)
        {
            return language.ToUpper() == CSharp.ToUpper()
                || language.ToUpper() == Executable.ToUpper();
        }
    }
}