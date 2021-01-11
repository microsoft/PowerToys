namespace Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.SshConfigParser
{
    public class ConfigNode
    {
        public string Before { get; set; }
        public string After { get; set; }
        public NodeType Type { get; set; }
        public string Content { get; set; }
        public string Param { get; set; }
        public string Separator { get; set; }
        public string Value { get; set; }
        public bool Quoted { get; set; }
        public SshConfig Config { get; set; }
    }
}