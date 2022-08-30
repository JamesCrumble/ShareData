using System;
using ExileCore;
using System.Linq;
using ExileCore.Shared.Helpers;
using System.Collections.Generic;
using ExileCore.PoEMemory.MemoryObjects;

using System.IO;
using Newtonsoft.Json;
using System.Reflection;

class DataBuilder
{
    internal static ShareDataContent updatedData = new ShareDataContent();

    internal static Dictionary<string, PreloadConfigLine> PreloadConfig = new Dictionary<string, PreloadConfigLine>();
    internal static List<string> ReloadGameFilesMethods = new List<string>();


    public static void ReadConfigFiles(string PreloadAlerts, string PreloadAlertsPersonal)
    {
        if (!File.Exists(PreloadAlerts))
        {
            DebugWindow.LogError($"PreloadAlert.ReadConfigFiles -> Config file is missing: {PreloadAlerts}");
            return;
        }
        if (!File.Exists(PreloadAlertsPersonal))
        {
            File.Create(PreloadAlertsPersonal);
            DebugWindow.LogMsg($"PreloadAlert.ReadConfigFiles -> Personal config file got created: {PreloadAlertsPersonal}");
        }

        DataBuilder.PreloadConfig.Clear();

        AddLinesFromFile(PreloadAlerts, PreloadConfig);

        AddLinesFromFile(PreloadAlertsPersonal, PreloadConfig);
    }

    public static void InitReloadFilesMethods()
    {
        ReloadGameFilesMethods.Add("LoadFiles");
        ReloadGameFilesMethods.Add("ReloadFiles");
    }

    public static void AddLinesFromFile(string path, IDictionary<string, PreloadConfigLine> preloadLines)
    {
        if (!File.Exists(path)) return;

        var lines = File.ReadAllLines(path);
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#")) continue;

            var lineContent = line.Split(';');
            var metadataKey = lineContent[0].Trim();
            if (preloadLines.ContainsKey(metadataKey))
            {
                if (line.StartsWith("-"))
                {
                    preloadLines.Remove(metadataKey);
                }
                continue;
            }

            var textAndColor = new PreloadConfigLine
            {
                Text = lineContent[1].Trim(),
                Color = lineContent.ConfigColorValueExtractor(2)
            };
            preloadLines.Add(metadataKey, textAndColor);
        }
    }

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

                if (file.Key.Contains("Archnemesis") || file.Key.Contains("LeagueBestiary"))
                {
                    Entity entity = Controller.Entities.FirstOrDefault(e => e.Metadata.Equals(file.Key) || e.Metadata.Equals(text));

                    if (entity != null)
                    {
                        DebugWindow.LogMsg($"{entity}");
                        DebugWindow.LogMsg($"IsValid - {entity.IsValid}");
                        DebugWindow.LogMsg($"IsDead - {entity.IsDead}");
                        DebugWindow.LogMsg($"IsHidden - {entity.IsHidden}");
                        DebugWindow.LogMsg($"IsAlive - {entity.IsAlive}");
                    }
                }

                var preloadLine = PreloadConfig.FirstOrDefault(tuple => text == tuple.Key);
                // if (text.Contains("LeagueBestiary")) { 
                //    DebugWindow.LogMsg($"{file.Key} - {preloadLine.Value} - {preloadLine.Value != null} - {file.Value.ChangeCount}");
                // }

                if (preloadLine.Value == null) continue;
                // data.Add(preloadLine.Value.Text.Replace("\"", "") + " " + file.Key);
                data.Add(preloadLine.Value.Text.Replace("\"", ""));
            }
            // using (StreamWriter writer = new StreamWriter("content.json"))
            // {
            //    writer.WriteLine("[\n");
            //    foreach (var entity in Controller.Entities) {
            //        writer.WriteLine(entity);
            //        writer.WriteLine($"MetaData - {entity.Metadata}");
            //        writer.WriteLine($"IsValid - {entity.IsValid}");
            //        writer.WriteLine($"IsDead - {entity.IsDead}");
            //        writer.WriteLine($"IsHidden - {entity.IsHidden}");
            //        writer.WriteLine($"IsAlive - {entity.IsAlive}");
            //        writer.WriteLine();
            //    }
            //    writer.WriteLine("]");
            // }
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

    public static void UpdateContentData(GameController Controller)
    {
        ShareDataContent content = new ShareDataContent();
        //DebugWindow.LogMsg($"{Controller.IngameState.ServerData.Address}");
        //DebugWindow.LogMsg($"{String.Join(", ", Controller.IngameState.ServerData.NearestPlayers)}");
        //DebugWindow.LogMsg($"{Controller.IngameState.ServerData.NetworkState}");

        if (Controller != null)
        {
            content.items_on_ground_label = BuildItemsOnGroundLabels(Controller);
            content.player_data = ParsePlayerData(Controller);
            content.current_location = Controller.Area.CurrentArea.DisplayName;
            //if (oldMap != Controller.Area.CurrentArea.DisplayName)
            //{
            //    //DebugWindow.LogMsg("Before update");
            //    content.location_content = ParseLocationContentData(Controller);
            //    //DebugWindow.LogMsg("After update");
            //    oldMap = Controller.Area.CurrentArea.DisplayName;
            //}
            content.location_content = ParseLocationContentData(Controller);

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