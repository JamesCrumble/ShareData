using System.Collections.Generic;

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
    public string mouse_position;
}