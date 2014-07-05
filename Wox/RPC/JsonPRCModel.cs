using Wox.Plugin;

namespace Wox.RPC
{
    public class JsonPRCModel
    {
        public int id { get; set; }
        public string jsonrpc { get; set; }

        public string result { get; set; }
    }

    public class ActionJsonRPCResult : Result
    {
        public string ActionJSONRPC { get; set; }
    }
}
