using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveLoadManager
{
    private static readonly string DefaultSaveFileName = "DrawingData";

    private static string currentFileName;
    private static bool isDirty = false;
    
    public static bool IsDirty() => isDirty;
    public static void Clear() => currentFileName = string.Empty;
    public static void MakeDirty()
    {
        isDirty = true;
    }
    
    public static bool IsFileLoaded()
    {
        if (string.IsNullOrEmpty(currentFileName)) return false;
        Debug.Log("Current File Name " +currentFileName);
        return true;
    }
    
    public static void Save()
    {
        Debug.Log("Save without parameter");
        Save(currentFileName);
    }
    
    public static void Save(string customName = null)
    {
        Debug.Log("Save with parameter");
        SaveData saveData = new SaveData
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            paths = new List<SavedPath>(),
            furnitureDatas = FurnitureManager.GetAllFurnitureData()
        };

        // Save Rooms
        foreach (Room room in RoomStorage.rooms)
        {
            var path = new SavedPath
            {
                roomID = room.ID,
                groupID = room.groupID,
                roomName = room.roomName,
                floorMaterial = room.floorMaterial,
                floorID = room.floorID,
                points = room.checkpoints.ConvertAll(p => new Vector2Serializable(p)),
                pointsExtra = room.extraCheckpoints.ConvertAll(p => new Vector2Serializable(p)),
                heights = new List<float>(room.heights),
                wallLines = room.wallLines.ConvertAll(w => new SavedWallLine
                {
                    start = w.start,
                    end = w.end,
                    type = w.type,
                    isVisible = w.isVisible,
                    distanceHeight = w.distanceHeight,
                    Height = w.Height,
                    isManualConnection = w.isManualConnection,
                    headingCompass=w.headingCompass,
                    materialFront = w.materialFront,
                    materialBack = w.materialBack
                }),
                compass = new Vector2Serializable(room.Compass),
                center = new Vector2Serializable(room.center),
                headingCompass = room.headingCompass
            };
            saveData.paths.Add(path);
        }

        // Save Floors
        saveData.floors = new List<SavedFloor>();
        foreach (Floor floor in FloorStorage.floors)
        {
            var f = new SavedFloor
            {
                floorID = floor.ID,
                floorName = floor.floorName,
                points = floor.checkpoints.ConvertAll(p => new Vector2Serializable(p)),
                heights = new List<float>(floor.heights),
                floorLine = floor.floorLine.ConvertAll(fl => new SavedFloorLine
                {
                    start = fl.start,
                    end = fl.end
                }),
                center = new Vector2Serializable(floor.center),
                roomIDs = new List<string>(floor.roomIDs)
            };
            saveData.floors.Add(f);
        }

        // Save file
        string baseName = string.IsNullOrEmpty(customName) ? DefaultSaveFileName : customName;
        string fileName = $"{baseName}.json";
        string pathToSave = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(pathToSave, JsonUtility.ToJson(saveData, true));

        Debug.Log($"[Save] OK: {pathToSave}");
        currentFileName = baseName;
        isDirty = false;
    }

    // ==== LOAD ====
    public static void Load(string fileName = "DrawingData.json")
    {
        string pathToLoad = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(pathToLoad))
        {
            Debug.LogWarning("[Load] Không có file: " + pathToLoad);
            return;
        }

        string json = File.ReadAllText(pathToLoad);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

        // Clear current
        RoomStorage.rooms = new List<Room>();
        FloorStorage.floors = new List<Floor>();
        FurnitureManager.AddFurnitures(saveData.furnitureDatas);

        // Load Rooms
        foreach (var path in saveData.paths)
        {
            Room room = new Room();
            room.SetID(path.roomID);
            room.groupID = path.groupID;
            room.roomName = path.roomName;
            room.floorMaterial = path.floorMaterial;
            room.floorID = path.floorID;
            room.checkpoints = path.points.ConvertAll(p => p.ToVector2());
            room.extraCheckpoints = path.pointsExtra.ConvertAll(p => p.ToVector2());
            room.heights = new List<float>(path.heights);
            room.wallLines = path.wallLines.ConvertAll(w =>
            {
                var line = new WallLine(w.start, w.end, w.type, w.distanceHeight, w.Height, w.materialFront, w.materialBack);
                line.isManualConnection = w.isManualConnection;
                line.headingCompass = w.headingCompass;
                line.isVisible = w.isVisible;
                return line;
            });
            room.Compass = path.compass.ToVector2();
            room.center = path.center.ToVector2();
            room.headingCompass = path.headingCompass;
            RoomStorage.rooms.Add(room);
        }

        // Load Floors
        if (saveData.floors != null)
        {
            foreach (var f in saveData.floors)
            {
                Floor floor = new Floor();
                floor.SetID(f.floorID);
                floor.floorName = f.floorName;
                floor.checkpoints = f.points.ConvertAll(p => p.ToVector2());
                floor.heights = new List<float>(f.heights);
                floor.floorLine = f.floorLine.ConvertAll(fl => new FloorLine(fl.start, fl.end));
                floor.center = f.center.ToVector2();
                floor.roomIDs = new List<string>(f.roomIDs);
                FloorStorage.floors.Add(floor);
            }
        }

        currentFileName = RemoveExtension(fileName);
        Debug.Log($"[Load] Loaded {RoomStorage.rooms.Count} rooms + {FloorStorage.floors.Count} floors from: {fileName}");

        SceneManager.LoadScene("FlatExampleScene");
    }

    public static bool DoesNameExist(string baseName)
    {
        string folderPath = Application.persistentDataPath;
        string[] files = Directory.GetFiles(folderPath, "*.json");

        foreach (string path in files)
        {
            try
            {
                string json = File.ReadAllText(path);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(baseName))
                {
                    return true;
                }

                //SaveData data = JsonUtility.FromJson<SaveData>(json);
                //Debug.Log($"File Path {path}");
                //if (data.paths.Count > 0 && data.paths[0].roomID == baseName)
                //    return true;
            }
            catch
            {
                Debug.LogWarning("Bỏ qua file lỗi: " + path);
            }
        }

        return false;
    }
    
    public static bool IsContainSaveFileLocal()
    {
        List<JsonFileInfo> infos = new List<JsonFileInfo>();
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");
        return files.Length > 0;
    }

    public static List<JsonFileInfo> GetAllSavedFileInfos()
    {
        List<JsonFileInfo> infos = new List<JsonFileInfo>();
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");

        foreach (string path in files)
        {
            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                string fileName = Path.GetFileName(path);
                string name = (data.paths.Count > 0) ? data.paths[0].roomID : fileName;
                string time = data.timestamp;

                infos.Add(new JsonFileInfo
                {
                    fileName = fileName,
                    displayName = name,
                    timestamp = time
                });
            }
            catch
            {
                Debug.LogWarning("Bỏ qua file lỗi: " + path);
            }
        }

        return infos;
    }


    public static bool TryDeleteFile(string fileName)
    {
        try
        {
            string fullFileName = $"{fileName}.json";
            string fullFilePath = Path.Combine(Application.persistentDataPath, fullFileName);

            Debug.Log($"Input file name {fileName}");
            Debug.Log($"Full file name {fullFileName}");
            Debug.Log($"Full file path {fullFilePath}");

            if (!File.Exists(fullFilePath))
            {
                return false;
            }

            //
            Debug.Log("Delete file");
            File.Delete(fullFilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error Log {e.Message}");
            throw;
        }
    }

    public static bool ChangeFileName(string currentFileName, string newFileName)
    {
        if (string.IsNullOrWhiteSpace(currentFileName) || string.IsNullOrWhiteSpace(newFileName))
        {
            Debug.LogError("[ChangeFileName] File names must not be empty or whitespace.");
            return false;
        }

        try
        {
            currentFileName = EnsureJsonExtension(currentFileName);
            newFileName = EnsureJsonExtension(newFileName);

            string oldFilePath = Path.Combine(Application.persistentDataPath, currentFileName);
            string newFilePath = Path.Combine(Application.persistentDataPath, newFileName);

            if (!File.Exists(oldFilePath))
            {
                Debug.LogError($"[ChangeFileName] Old file path '{oldFilePath}' does not exist.");
                return false;
            }

            if (File.Exists(newFilePath))
            {
                Debug.LogError($"[ChangeFileName] New file path '{newFilePath}' already exists.");
                return false;
            }

            File.Move(oldFilePath, newFilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ChangeFileName] Unexpected error: {e.Message}");
            return false;
        }
    }


    public static string EnsureJsonExtension(string fileName)
    {
        return EnsureExtension(fileName, ".json");
    }

    private static string EnsureExtension(string fileName,string extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            fileName += extension;
        }

        return fileName;
    }

    private static string RemoveExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        int dotIndex = fileName.LastIndexOf('.');
        if (dotIndex > 0)
        {
            return fileName.Substring(0, dotIndex);
        }

        return fileName; 
    }
}
