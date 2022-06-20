using ExileCore.Shared.Nodes;
using ExileCore.Shared.Interfaces;

namespace ShareData
{
    public class ShareDataSettings : ISettings
    {
        public ShareDataSettings()
        {
            Enable = new ToggleNode(true);
            Port = new TextNode("50000");
        }
        public ToggleNode Enable { get; set; }
        public TextNode Port { get; set; }
    }
}
