namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    sealed class CamelNameSelector : INameSelector
    {
        public static readonly INameSelector Instance = new CamelNameSelector();

        private CamelNameSelector()
        {
        }

        public String Select(IEnumerable<String> registered, MemberInfo member)
        {
            var name = member.Name;

            if (member is Type == false)
            {
                if (member is ConstructorInfo)
                {
                    name = "create";
                }
                else if (member is PropertyInfo && name == "Item")
                {
                    name = "at";
                }
                else
                {
                    name = ConvertToCamelCase(name);
                }
            }

            while (registered.Contains(name))
            {
                name = "_" + name;
            }

            return name;
        }

        private static String ConvertToCamelCase(String str)
        {
            if (str != null)
            {
                if (str.Length > 1)
                {
                    var words = str.Split(new [] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    var result = String.Concat(words[0].Substring(0, 1).ToLowerInvariant(), words[0].Substring(1));

                    for (var i = 1; i < words.Length; i++)
                    {
                        result = String.Concat(result, words[i].Substring(0, 1).ToUpperInvariant(), words[i].Substring(1));
                    }

                    return result;
                }

                return str.ToLowerInvariant();
            }

            return str;
        }
    }
}
