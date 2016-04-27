using System;

namespace Wox.Infrastructure
{
    static class Helper
    {
        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T RequireNonNull<T>(this T obj)
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
