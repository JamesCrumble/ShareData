using ExileCore.Shared.Nodes;
using ExileCore.Shared.Interfaces;

namespace ShareData
{
    public class ShareDataSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new(true);
        public TextNode Port { get; set; } = new("50000");
    }
}
