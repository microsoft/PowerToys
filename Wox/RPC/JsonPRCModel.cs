using System.Collections.Generic;
using System.Windows.Documents;
using Newtonsoft.Json;
using Wox.Plugin;

namespace Wox.RPC
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
        public List<JsonRPCResult> QueryResults
        {
            get
            {
                return JsonConvert.DeserializeObject<List<JsonRPCResult>>(Result);
            }
        }
    }

    public class JsonRPCRequestModel : JsonRPCModelBase
    {
        public string Method { get; set; }

        /* 
         * 1. c# can't use params as the variable name
         * 2. all prarmeter should be string type
         */
        public List<string> Parameters { get; set; }
    }

    public class JsonRPCResult : Result
    {
        public string JSONRPCAction { get; set; }

        public JsonRPCRequestModel JSONRPCActionModel
        {
            get { return null; }
        }
    }
}
