using System;

namespace Wox.Helper
{
    public static class SyntaxSugars
    {
        public static TResult CallOrRescueDefault<TResult>(Func<TResult> callback)
        {
            return CallOrRescueDefault(callback, default(TResult));
        }

        public static TResult CallOrRescueDefault<TResult>(Func<TResult> callback, TResult def)
        {
            try
            {
                return callback();
            }
            catch
            {
                return def;
            }
        }
    }
}
