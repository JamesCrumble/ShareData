using System;
using ExileCore;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ExileCore.PoEMemory.MemoryObjects;

using Newtonsoft.Json;
using System.Reflection;

class DataBuilder
{
    internal static bool isAreaChanged = false;
    internal static ShareDataContent updatedData = new ShareDataContent();
    internal static ConcurrentDictionary<string, PreloadConfigLine> PreloadConfigLines = new ConcurrentDictionary<string, PreloadConfigLine>();
    internal static List<string> ReloadGameFilesMethods = new List<string>();

    public static bool DynamicLinkingReloadGameFiles(GameController Controller) 
    {
        Type t = Controller.Files.GetType();


        foreach (var MethodName in ReloadGameFilesMethods)
        {

            DebugWindow.LogMsg(MethodName);
            MethodInfo Method = t.GetMethod(MethodName);

            if (Method != null) 
            { 
                Method.Invoke(Controller.Files, null);
                return true;
            }
        }

        return false;
    }

    public static List<string> ParseLocationContentData(GameController Controller)
    {

        List<string> data = new List<string>();

        while (true)
        { 
            if (!Controller.IsLoading)
            {
                break;
            }
        }

        try
        {
            if (!DynamicLinkingReloadGameFiles(Controller)) { return data; }
            
            var allFiles = Controller.Files.AllFiles;

            foreach (var file in allFiles)
            {
                if (file.Value.ChangeCount != Controller.Game.AreaChangeCount) continue;

                var text = file.Key;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (text.Contains("@")) text = text.Split('@')[0];

                text = text.Trim();


                var preloadLine = PreloadConfigLines.FirstOrDefault(tuple => text == tuple.Key);
                if (text.Contains("LeagueBestiary")) { 
                    DebugWindow.LogMsg($"{text} - {preloadLine} - {preloadLine.Value} - {preloadLine.Value != null}");
                }

                if (preloadLine.Value == null) continue;
                data.Add(preloadLine.Value.Text.Replace("\"", ""));
            }
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(DataBuilder)} -> {e}");
        }

        return data;
    }

    public static string ContentAsJson()
    {
        return JsonConvert.SerializeObject(updatedData, Formatting.Indented);
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

    public static List<string> UpdateLocationContent(GameController Controller)
    {
        List<string> values = new List<string>();
        return values;
    }

    public static void UpdateContentData(GameController Contoller)
    {
        ShareDataContent content = new ShareDataContent();

        if (Contoller != null)
        {
            content.items_on_ground_label = BuildItemsOnGroundLabels(Contoller);
            content.player_data = ParsePlayerData(Contoller);
            content.current_location = Contoller.Area.CurrentArea.DisplayName;
            DebugWindow.LogMsg("Before update");
            content.location_content = ParseLocationContentData(Contoller);
            DebugWindow.LogMsg("After update");

            //if (Contoller.Area.CurrentArea.DisplayName != updatedData.current_location) 
            //{
            //    isAreaChanged = true;
            //}
            //
            //if (isAreaChanged) 
            //{ 
            //    isAreaChanged = false;
            //    content.location_content = ParseLocationContentData(Contoller);
            //    //Core.ParallelRunner.Run(
            //    //    new Coroutine(ParseLocationContentData(Contoller), $"{nameof(DataBuilder)}")
            //    //);
            //}
        }

        updatedData = content;
    }
}