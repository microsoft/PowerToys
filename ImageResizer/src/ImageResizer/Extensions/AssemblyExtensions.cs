using System.Linq;

namespace System.Reflection
{
    static class AssemblyExtensions
    {
        public static T GetCustomAttribute<T>(this Assembly assembly)
            where T : Attribute
            => (T)assembly.GetCustomAttributes(typeof(T), inherit: false).SingleOrDefault();
    }
}
