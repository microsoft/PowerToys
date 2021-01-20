namespace Mages.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the plugin essentials.
    /// </summary>
    public class Plugin
    {
        #region Fields

        private readonly IDictionary<String, String> _metaData;
        private readonly IDictionary<String, Object> _content;

        #endregion
        
        #region ctor

        /// <summary>
        /// Creates a new plugin.
        /// </summary>
        public Plugin(IDictionary<String, String> metaData, IDictionary<String, Object> content)
        {
            _metaData = metaData;
            _content = content;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public String Name
        {
            get
            {
                var result = default(String);
                _metaData.TryGetValue("name", out result);
                return result;
            }
        }

        /// <summary>
        /// Gets the plugin's meta data.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>> MetaData
        {
            get { return _metaData; }
        }

        /// <summary>
        /// Gets the plugin's content.
        /// </summary>
        public IEnumerable<KeyValuePair<String, Object>> Content
        {
            get { return _content; }
        }

        #endregion
    }
}
