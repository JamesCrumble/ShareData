using System;
using ExileCore;
using System.Linq;
using ExileCore.Shared.Helpers;
using System.Collections.Generic;
using ExileCore.PoEMemory.MemoryObjects;

using Newtonsoft.Json;
using System.Reflection;

class PreloadBuilder
{

}

class DataBuilder
{
    internal static ShareDataContent updatedData = new();

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

    private static List<string> ParseLocationContentData(GameController Controller)
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
            mouse_position = BuildMousePositionData(Controller),
            location_content = ParseLocationContentData(Controller),
            current_location = Controller.Area.CurrentArea.DisplayName,
        };

        updatedData = content;
    }
}