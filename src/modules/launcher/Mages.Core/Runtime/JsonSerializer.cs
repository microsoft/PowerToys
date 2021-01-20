namespace Mages.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    sealed class JsonSerializer
    {
        private readonly HashSet<Object> _seen;

        public JsonSerializer()
        {
            _seen = new HashSet<Object>();
        }

        public String Serialize(Object value)
        {
            var sb = StringBuilderPool.Pull();
            SerializeTo(value, sb, 0);
            return sb.Stringify();
        }

        private void SerializeTo(IDictionary<String, Object> obj, StringBuilder buffer, Int32 level)
        {
            var index = 0;
            _seen.Add(obj.Unwrap());
            buffer.AppendLine("{");

            foreach (var item in obj)
            {
                var sublevel = level + 1;
                var key = Stringify.AsJson(item.Key);
                buffer.Append(' ', 2 * sublevel).Append(key).Append(": ");
                SerializeTo(item.Value, buffer, sublevel);

                if (index + 1 < obj.Count)
                {
                    buffer.Append(',');
                }

                buffer.AppendLine();
            }

            buffer.Append(' ', 2 * level).Append('}');
        }

        private void SerializeTo(Object value, StringBuilder buffer, Int32 level)
        {
            if (value == null)
            {
                buffer.Append("null");
            }
            else if (value is Function)
            {
                buffer.Append(Stringify.AsJson("[Function]"));
            }
            else if (value is IDictionary<String, Object>)
            {
                if (!_seen.Contains(value.Unwrap()))
                {
                    SerializeTo((IDictionary<String, Object>)value, buffer, level);
                }
                else
                {
                    buffer.Append(Stringify.AsJson("[Recursion]"));
                }
            }
            else if (value is Double[,])
            {
                buffer.Append(Stringify.AsJson((Double[,])value));
            }
            else if (value is String)
            {
                buffer.Append(Stringify.AsJson((String)value));
            }
            else if (value is Double)
            {
                buffer.Append(Stringify.This((Double)value));
            }
            else if (value is Boolean)
            {
                buffer.Append(Stringify.This((Boolean)value));
            }
            else
            {
                buffer.Append("undefined");
            }
        }
    }
}
