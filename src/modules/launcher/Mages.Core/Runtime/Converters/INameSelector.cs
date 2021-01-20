namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Represents the interface to handle name selections.
    /// </summary>
    public interface INameSelector
    {
        /// <summary>
        /// Selects a name for the given member.
        /// </summary>
        /// <param name="registered">The already registered names.</param>
        /// <param name="member">The member to give a MAGES name.</param>
        /// <returns>The selected name.</returns>
        String Select(IEnumerable<String> registered, MemberInfo member);
    }
}
