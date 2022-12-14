using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Reflection;

class DataBuilder
{
    internal static ShareDataContent updatedData = new ShareDataContent();

    public static string ContentAsJson()
    {
        return JsonConvert.SerializeObject(updatedData, Formatting.Indented);
    }

    public static Dictionary<string, ShareDataEntity> BuildItemsOnGroundLabels(GameController Controller)
    {
        Dictionary<string, ShareDataEntity> dict = new Dictionary<string, ShareDataEntity>();

        try
        {
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
        } catch (Exception e)
        {
            DebugWindow.LogMsg($"ShareData cannot Cannot build ItemsOnGroundLabels data -> {e}");
        }

        return dict;
    }

    public static Dictionary<string, string> BuildServerData(GameController Controller) {
        Dictionary<string, string> dict = new Dictionary<string, string>();
        return dict;
    }

    public static ShareDataEntity ParsePlayerData(GameController Controller)
    {
        ShareDataEntity playerData = new ShareDataEntity();
        
        try
        {

            Entity PlayerData = Controller.EntityListWrapper.Player;

            playerData.bounds_center_pos = $"{PlayerData.BoundsCenterPos}";
            playerData.grid_pos = $"{PlayerData.GridPos}";
            playerData.pos = $"{PlayerData.Pos}";
            playerData.distance_to_player = $"{PlayerData.DistancePlayer}";
            playerData.on_screen_position = $"{Controller.IngameState.Camera.WorldToScreen(PlayerData.BoundsCenterPos)}";
            playerData.additional_info = $"{PlayerData}";

        }
        catch (Exception e) {
            DebugWindow.LogMsg($"ShareData cannot build player data -> {e}");
        }
        return playerData;
    }

    public static string BuildMousePositionData(GameController Controller)
    {
        try
        {
            var mousePosX = Controller.IngameState.GetType().GetProperty("MousePosX").GetValue(Controller.IngameState);
            var mousePosY = Controller.IngameState.GetType().GetProperty("MousePosY").GetValue(Controller.IngameState);

            return $"X:{mousePosX} Y:{mousePosY}";
        }
        catch (Exception e) {
            DebugWindow.LogMsg($"ShareData cannot build mouse position data -> {e}");
        }

        return "";
    }

    public static void UpdateContentData(GameController Controller)
    {
        ShareDataContent content = new ShareDataContent();

        content.items_on_ground_label = BuildItemsOnGroundLabels(Controller);
        content.player_data = ParsePlayerData(Controller);
        content.mouse_position = BuildMousePositionData(Controller);

        updatedData = content;
    }
}