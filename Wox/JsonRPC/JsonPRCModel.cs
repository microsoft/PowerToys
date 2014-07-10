using System.Collections.Generic;
using System.Linq;
using Wox.Plugin;

namespace Wox.JsonRPC
{
    public class JsonRPCErrorModel
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }

    public class JsonRPCModelBase
    {
        public int Id { get; set; }

        public string JsonRPC { get; set; }
    }

    public class JsonRPCResponseModel : JsonRPCModelBase
    {
        public string Result { get; set; }

        public JsonRPCErrorModel Error { get; set; }
    }

    public class JsonRPCQueryResponseModel : JsonRPCResponseModel
    {
        public new List<JsonRPCResult> Result { get; set; }
    }

    public class JsonRPCRequestModel : JsonRPCModelBase
    {
        public string Method { get; set; }

        /// <summary>
        /// counld be list<string> or string type
        /// </summary>
        public object[] Parameters { get; set; }

        public bool DontHideAfterAction { get; set; }

        public override string ToString()
        {
            if (Parameters != null && Parameters.Length > 0)
            {
                string parameters = Parameters.Aggregate("[", (current, o) => current + (GetParamterByType(o) + ","));
                parameters = parameters.Substring(0, parameters.Length - 1) + "]";
                return string.Format(@"{{\""method\"":\""{0}\"",\""parameters\"":{1}}}", Method, parameters);
            }

            return string.Format(@"{{\""method\"":\""{0}\"",\""parameters\"":[]}}",Method);
        }

        private string GetParamterByType(object paramter)
        {
            if (paramter is string)
            {
                return string.Format(@"\""{0}\""", paramter);
            }
            if (paramter is int || paramter is float || paramter is double)
            {
                return string.Format(@"{0}", paramter);
            }
            if (paramter is bool)
            {
                return string.Format(@"{0}", paramter.ToString().ToLower());
            }
            return paramter.ToString();
        }
    }

    public class JsonRPCResult : Result
    {
        public JsonRPCRequestModel JsonRPCAction { get; set; }
    }
}
