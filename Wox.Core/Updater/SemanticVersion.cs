using System;
using Wox.Infrastructure.Exception;

namespace Wox.Core.Updater
{
    public class SemanticVersion : IComparable
    {
        public int MAJOR { get; set; }
        public int MINOR { get; set; }
        public int PATCH { get; set; }

        public SemanticVersion(System.Version version)
        {
            MAJOR = version.Major;
            MINOR = version.Minor;
            PATCH = version.Build;
        }

        public SemanticVersion(int major, int minor, int patch)
        {
            MAJOR = major;
            MINOR = minor;
            PATCH = patch;
        }

        public SemanticVersion(string version)
        {
            var strings = version.Split('.');
            if (strings.Length != 3)
            {
                throw new WoxException("Invalid semantic version");
            }
            MAJOR = int.Parse(strings[0]);
            MINOR = int.Parse(strings[1]);
            PATCH = int.Parse(strings[2]);
        }

        public static bool operator >(SemanticVersion v1, SemanticVersion v2)
        {
            return v1.CompareTo(v2) > 0;
        }

        public static bool operator <(SemanticVersion v1, SemanticVersion v2)
        {
            return v1.CompareTo(v2) < 0;
        }

        public static bool operator ==(SemanticVersion v1, SemanticVersion v2)
        {
            if (ReferenceEquals(v1, null))
            {
                return ReferenceEquals(v2, null);
            }
            if (ReferenceEquals(v2, null))
            {
                return false;
            }
            return v1.Equals(v2);
        }

        public static bool operator !=(SemanticVersion v1, SemanticVersion v2)
        {
            return !(v1 == v2);
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}", MAJOR, MINOR, PATCH);
        }

        public override bool Equals(object version)
        {
            var v2 = (SemanticVersion)version;
            return MAJOR == v2.MAJOR && MINOR == v2.MINOR && PATCH == v2.PATCH;
        }

        public int CompareTo(object version)
        {
            var v2 = (SemanticVersion)version;
            if (MAJOR == v2.MAJOR)
            {
                if (MINOR == v2.MINOR)
                {
                    if (PATCH == v2.PATCH)
                    {
                        return 0;
                    }
                    return PATCH - v2.PATCH;
                }
                return MINOR - v2.MINOR;
            }
            return MAJOR - v2.MAJOR;
        }
    }
}
