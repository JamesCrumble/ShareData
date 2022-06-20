using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

using System.Collections.Generic;
using Newtonsoft.Json;

class DataBuilder
{
    internal static ShareDataContent updatedData = new ShareDataContent();

    public static string ShareDataContentToJson(ShareDataContent content)
    {
        return JsonConvert.SerializeObject(content, Formatting.Indented);
    }

    public static Dictionary<string, ShareDataEntity> BuildItemsOnGroundLabels(GameController Controller)
    {
        Dictionary<string, ShareDataEntity> dict = new Dictionary<string, ShareDataEntity>();

        foreach (var value in Controller.IngameState.IngameUi.ItemsOnGroundLabels)
        {
            ShareDataEntity entity = new ShareDataEntity();

            entity.bounds_center_pos = $"{value.ItemOnGround.BoundsCenterPos}";
            entity.grid_pos = $"{value.ItemOnGround.GridPos}";
            entity.pos = $"{value.ItemOnGround.Pos}";
            entity.distance_to_player = $"{value.ItemOnGround.DistancePlayer}";
            entity.on_screen_position = $"{Controller.IngameState.Camera.WorldToScreen(value.ItemOnGround.BoundsCenterPos)}";
            entity.additional_info = $"{value.ItemOnGround}";

            dict.Add($"{value.ItemOnGround.Type}-{value.ItemOnGround.Address:X}", entity);

        }

        return dict;
    }

    public static ShareDataEntity ParsePlayerData(GameController Controller)
    {
        Entity PlayerData = Controller.EntityListWrapper.Player;
        ShareDataEntity playerData = new ShareDataEntity();

        playerData.bounds_center_pos = $"{PlayerData.BoundsCenterPos}";
        playerData.grid_pos = $"{PlayerData.GridPos}";
        playerData.pos = $"{PlayerData.Pos}";
        playerData.distance_to_player = $"{PlayerData.DistancePlayer}";
        playerData.on_screen_position = $"{Controller.IngameState.Camera.WorldToScreen(PlayerData.BoundsCenterPos)}";
        playerData.additional_info = $"{PlayerData}";

        return playerData;
    }

    public static void UpdateContentData(GameController Contoller)
    {
        ShareDataContent content = new ShareDataContent();

        content.items_on_ground_label = BuildItemsOnGroundLabels(Contoller);
        content.player_data = ParsePlayerData(Contoller);
        content.mouse_position = $"X:{Contoller.IngameState.MousePosX} Y:{Contoller.IngameState.MousePosY}";

        updatedData = content;
    }
}