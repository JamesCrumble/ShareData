public struct ShareDataEntity
{
    public string bounds_center_pos;
    public string grid_pos;
    public string pos;
    public string distance_to_player;
    public string on_screen_position;
    public string additional_info;
}

public struct ShareDataContent
{
    public Dictionary<string, ShareDataEntity> items_on_ground_label;
    public ShareDataEntity player_data;
    public string current_location;
    public List<string> location_content;
}

public abstract class ConfigLineBase
{
    public string Text { get; set; }
    public Color? Color { get; set; }

    public override bool Equals(object obj)
    {
        return Text == ((ConfigLineBase)obj).Text;
    }

    public override int GetHashCode()
    {
        return Text.GetHashCode();
    }
}

public class PreloadConfigLine : ConfigLineBase
{
    public Func<Color> FastColor { get; set; }
}