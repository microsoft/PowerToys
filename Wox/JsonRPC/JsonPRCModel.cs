using System.Collections.Generic;
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
        public object Parameters { get; set; }

        public override string ToString()
        {
            if (Parameters is string)
            {
                return string.Format(@"{{\""method\"":\""{0}\"",\""parameters\"":\""{1}\""}}", Method, Parameters);
            }

            return string.Empty;
        }
    }

    public class JsonRPCResult : Result
    {
        public JsonRPCRequestModel JsonRPCAction { get; set; }
    }
}
