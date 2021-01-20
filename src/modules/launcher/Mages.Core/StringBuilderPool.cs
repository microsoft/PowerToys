namespace Mages.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A pool for recycled resources.
    /// </summary>
    static class StringBuilderPool
    {
        #region Fields

		private static readonly Stack<StringBuilder> _builder = new Stack<StringBuilder>();
        private static readonly Object _lock = new Object();

        #endregion

        #region Methods

        /// <summary>
        /// Either creates a fresh stringbuilder or gets a (cleaned) used one.
        /// </summary>
        /// <returns>A stringbuilder to use.</returns>
        public static StringBuilder Pull()
        {
            lock (_lock)
            {
                if (_builder.Count != 0)
                {
                    var builder = _builder.Pop();
                    builder.Length = 0;
                    return builder;
                }
                
                return new StringBuilder();                
            }
        }

        /// <summary>
        /// Returns the given stringbuilder to the pool and gets the current
        /// string content.
        /// </summary>
        /// <param name="sb">The stringbuilder to recycle.</param>
        /// <returns>The string that is contained in the stringbuilder.</returns>
        public static String Stringify(this StringBuilder sb)
        {
            lock (_lock)
            {
                var result = sb.ToString();

                if (_builder.Count < 4)
                {
                    _builder.Push(sb);
                }

                return result;
            }
        }

        #endregion
    }
}
