using BinaryAnalysis.UnidecodeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Wox.Plugin.BrowserBookmark
{
    public class Bookmark : IEquatable<Bookmark>, IEqualityComparer<Bookmark>
    {
        private string m_Name;
        public string Name
        {
            get
            {
                return m_Name;
            }
            set
            {
                m_Name = value;
                PinyinName = m_Name.Unidecode();
            }
        }
        public string PinyinName { get; private set; }
        public string Url { get; set; }
        public string Source { get; set; }
        public int Score { get; set; }

        /* TODO: since Source maybe unimportant, we just need to compare Name and Url */
        public bool Equals(Bookmark other)
        {
            return Equals(this, other);
        }

        public bool Equals(Bookmark x, Bookmark y)
        {
            if (Object.ReferenceEquals(x, y)) return true;
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.Name == y.Name && x.Url == y.Url;
        }

        public int GetHashCode(Bookmark bookmark)
        {
            if (Object.ReferenceEquals(bookmark, null)) return 0;
            int hashName = bookmark.Name == null ? 0 : bookmark.Name.GetHashCode();
            int hashUrl = bookmark.Url == null ? 0 : bookmark.Url.GetHashCode();
            return hashName ^ hashUrl;
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }
    }
}
