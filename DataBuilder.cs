using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

using Newtonsoft.Json;

class DataBuilder
{
    internal static ShareDataContent updatedData = new();

    public static string ContentAsJson()
    {
        return JsonConvert.SerializeObject(updatedData, Formatting.Indented);
    }

    private static Dictionary<string, ShareDataEntity> BuildItemsOnGroundLabels(GameController Controller)
    {
        Dictionary<string, ShareDataEntity> dict = new();

        try
        {
            foreach (var value in Controller.IngameState.IngameUi.ItemsOnGroundLabels)
            {
                ShareDataEntity entity = new()
                {
                    bounds_center_pos = $"{value.ItemOnGround.BoundsCenterPosNum}",
                    grid_pos = $"{value.ItemOnGround.GridPosNum}",
                    pos = $"{value.ItemOnGround.PosNum}",
                    distance_to_player = $"{value.ItemOnGround.DistancePlayer}",
                    on_screen_position = $"{Controller.IngameState.Camera.WorldToScreen(value.ItemOnGround.BoundsCenterPosNum)}",
                    additional_info = $"{value.ItemOnGround}"
                };

                dict.Add($"{value.ItemOnGround.Type}-{value.ItemOnGround.Address:X}", entity);

            }
        }
        catch (Exception e)
        {
            DebugWindow.LogMsg($"ShareData cannot Cannot build ItemsOnGroundLabels data -> {e}");
        }

        return dict;
    }

    public static Dictionary<string, string> BuildServerData(GameController Controller)
    {
        Dictionary<string, string> dict = new();
        return dict;
    }

    private static ShareDataEntity ParsePlayerData(GameController Controller)
    {
        try
        {

            Entity playerDataEntity = Controller.EntityListWrapper.Player;
            ShareDataEntity playerData = new()
            {
                bounds_center_pos = $"{playerDataEntity.BoundsCenterPosNum}",
                grid_pos = $"{playerDataEntity.GridPosNum}",
                pos = $"{playerDataEntity.PosNum}",
                distance_to_player = $"{playerDataEntity.DistancePlayer}",
                on_screen_position = $"{Controller.IngameState.Camera.WorldToScreen(playerDataEntity.BoundsCenterPosNum)}",
                additional_info = $"{playerDataEntity}"
            };
            return playerData;

        }
        catch (Exception e)
        {
            DebugWindow.LogMsg($"ShareData cannot build player data -> {e}");
        }

        return new ShareDataEntity();
    }

    private static string BuildMousePositionData(GameController Controller)
    {
        IngameState? ingameState = Controller.IngameState;
        if (ingameState is null) return "";

        try
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

            var mousePosX = ingameState.GetType().GetProperty("MousePosX").GetValue(ingameState);
            var mousePosY = ingameState.GetType().GetProperty("MousePosY").GetValue(ingameState);

#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return $"X:{mousePosX} Y:{mousePosY}";
        }
        catch (Exception e)
        {
            DebugWindow.LogMsg($"ShareData cannot build mouse position data -> {e}");
        }

        return "";
    }

    public static void UpdateContentData(GameController Controller)
    {
        ShareDataContent content = new()
        {
            items_on_ground_label = BuildItemsOnGroundLabels(Controller),
            player_data = ParsePlayerData(Controller),
            mouse_position = BuildMousePositionData(Controller)
        };

        updatedData = content;
    }
}