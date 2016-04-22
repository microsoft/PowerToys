using System;

namespace Wox.Infrastructure
{
    static class SyntaxSuger<T>
    {
        public static T RequireNonNull(T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return obj;
            }
        }
    }
}
