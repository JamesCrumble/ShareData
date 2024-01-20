using ExileCore;
using ExileCore.Shared.Helpers;
using ExileCore.PoEMemory.MemoryObjects;

using GameOffsets;

using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Threading;

class PreloadBuilder
{
    internal static uint AreaHash = 0;
    internal static Dictionary<string, PreloadConfigLine> PreloadConfig = new Dictionary<string, PreloadConfigLine>();
    internal static List<string> ReloadGameFilesMethods = new List<string>();
    private static List<string> locationContentCache = new();
    private static List<string> terrainEntitiesCache = new();

    public static void Initialise(string PreloadAlerts, string PreloadAlertsPersonal)
    {
        InitReloadFilesMethods();
        ReadConfigFiles(PreloadAlerts, PreloadAlertsPersonal);
    }

    private static void ReadConfigFiles(string PreloadAlerts, string PreloadAlertsPersonal)
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

        PreloadConfig.Clear();

        AddLinesFromFile(PreloadAlerts, PreloadConfig);

        AddLinesFromFile(PreloadAlertsPersonal, PreloadConfig);
    }

    private static void InitReloadFilesMethods()
    {
        ReloadGameFilesMethods.Add("LoadFiles");
        ReloadGameFilesMethods.Add("ReloadFiles");
    }

    private static void AddLinesFromFile(string path, IDictionary<string, PreloadConfigLine> preloadLines)
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

            var configLine = new PreloadConfigLine
            {
                Text = lineContent[1].Trim(),
                Color = lineContent.ConfigColorValueExtractor(2),
                TerrainEntity = lineContent.Length > 3,
            };
            preloadLines.Add(metadataKey, configLine);
        }
    }

    private static bool DynamicLinkingReloadGameFiles(GameController Controller)
    {
        Type t = Controller.Files.GetType();


        foreach (var MethodName in ReloadGameFilesMethods)
        {
            DebugWindow.LogMsg(MethodName);

            MethodInfo? Method = t.GetMethod(MethodName);
            if (Method is not null)
            {
                Method.Invoke(Controller.Files, null);
                return true;
            }
        }

        return false;
    }

    private static void waitWhileLoadingLocation(GameController Controller)
    {
        while (true)
        {
            if (!Controller.IsLoading)
            {
                break;
            }
        }
    }

    private static void BuildFromAllFilesContentData(GameController Controller)
    {
        foreach (var file in Controller.Files.AllFiles)
        {
            if (file.Value.ChangeCount != Controller.Game.AreaChangeCount) continue;


            string? text = file.Key;
            if (string.IsNullOrWhiteSpace(text)) continue;

            text = text.Split('@')[0].Trim();
            var preloadLine = PreloadConfig.FirstOrDefault(pair => text == pair.Key);
            if (preloadLine.Value is null) continue;

            if (preloadLine.Value.TerrainEntity) terrainEntitiesCache.Add(preloadLine.Key);
            locationContentCache.Add(preloadLine.Value.Text.Replace("\"", ""));
        }
    }

    private static void ExcludeUnexistedTerrainEntities(GameController Controller)
    {
        List<string> terrainContent = new();
        var tileData = Controller.Memory.ReadStdVector<TileStructure>(Controller.IngameState.Data.DataStruct.Terrain.TgtArray);

        for (int i = 0; i < tileData.Length; i++)
        {
            var tgtTileStruct = Controller.Memory.Read<TgtTileStruct>(tileData[i].TgtFilePtr);

            var key1 = tgtTileStruct.TgtPath.ToString(Controller.Memory);
            var key2 = Controller.Memory.Read<TgtDetailStruct>(tgtTileStruct.TgtDetailPtr).name.ToString(Controller.Memory);

            if (terrainEntitiesCache.Contains(key1)) terrainContent.Add(key1);
        }

        foreach (var terrainEntityKey in terrainEntitiesCache)
        {
            var preloadLine = PreloadConfig.FirstOrDefault(pair => terrainEntityKey == pair.Key);
            if (terrainContent.Contains(terrainEntityKey)) continue;
            locationContentCache.Remove(preloadLine.Value.Text);
        }
    }

    public static List<string> LocationContentData(GameController Controller)
    {
        if (AreaHash == Controller.Game.CurrentAreaHash) return locationContentCache;

        locationContentCache.Clear();
        terrainEntitiesCache.Clear();
        AreaHash = Controller.Game.CurrentAreaHash;
        waitWhileLoadingLocation(Controller);

        try
        {
            if (!DynamicLinkingReloadGameFiles(Controller)) { return locationContentCache; }
            BuildFromAllFilesContentData(Controller);
            if (terrainEntitiesCache.Count != 0) ExcludeUnexistedTerrainEntities(Controller);
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{nameof(DataBuilder)} -> {e}");
        }

        return locationContentCache;
    }
}

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
                    bounds_center_pos = Helpers.Vector3ToString(value.ItemOnGround.BoundsCenterPosNum),
                    grid_pos = Helpers.Vector2ToString(value.ItemOnGround.GridPosNum),
                    pos = Helpers.Vector3ToString(value.ItemOnGround.PosNum),
                    distance_to_player = $"{value.ItemOnGround.DistancePlayer}",
                    on_screen_position = Helpers.Vector2ToString(Controller.IngameState.Camera.WorldToScreen(value.ItemOnGround.BoundsCenterPosNum)),
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
                bounds_center_pos = $"{Helpers.Vector3ToString(playerDataEntity.BoundsCenterPosNum)}",
                grid_pos = $"{Helpers.Vector2ToString(playerDataEntity.GridPosNum)}",
                pos = $"{Helpers.Vector3ToString(playerDataEntity.PosNum)}",
                distance_to_player = $"{playerDataEntity.DistancePlayer}",
                on_screen_position = $"{Helpers.Vector2ToString(Controller.IngameState.Camera.WorldToScreen(playerDataEntity.BoundsCenterPosNum))}",
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
            current_location_hash = PreloadBuilder.AreaHash,
            location_content = PreloadBuilder.LocationContentData(Controller),
            current_location = Controller.Area.CurrentArea.DisplayName,
        };

        updatedData = content;
    }
}