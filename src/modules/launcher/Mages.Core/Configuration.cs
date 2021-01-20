namespace Mages.Core
{
    using Mages.Core.Ast;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the configuration DTO.
    /// </summary>
    public class Configuration
    {
        internal static readonly Configuration Default = new Configuration
        {
            Parser = new ExpressionParser(),
            Scope = null,
            IsEvalForbidden = false,
            IsEngineExposed = false,
            IsThisAvailable = false,
        };

        /// <summary>
        /// Gets or sets the parser to use.
        /// </summary>
        public IParser Parser
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the scope to use.
        /// </summary>
        public IDictionary<String, Object> Scope
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets of a global 'this' referencing the scope should be created.
        /// </summary>
        public Boolean IsThisAvailable
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets if the eval function is disallowed.
        /// </summary>
        public Boolean IsEvalForbidden
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets if the engine itself is exposed.
        /// </summary>
        public Boolean IsEngineExposed
        {
            get;
            set;
        }
    }
}
