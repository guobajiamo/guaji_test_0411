using System;
using System.IO;
using Godot;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 运行时可写路径辅助类。
/// 用来统一解决“导出后 res:// 只读，哪些文件应该改走 user://”的问题。
/// </summary>
public static class RuntimePathHelper
{
    public static string SaveDirectoryPath
    {
        get
        {
            if (OS.HasFeature("mobile") || OS.GetName().Equals("Android", StringComparison.OrdinalIgnoreCase))
            {
                return ProjectSettings.GlobalizePath("user://saves");
            }

            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Test00_0410");
        }
    }

    public static string SaveFilePath => GetTestSavePath();

    public const string RuntimeLogDirectory = "user://logs";
    public const string ItemTreeDumpPath = "user://logs/item_tree_dump.txt";

    public static string GetTestSavePath()
    {
        return Path.Combine(SaveDirectoryPath, "save_data.json");
    }

    public static string GetStorySaveSlotPath(int slotIndex)
    {
        int safeSlotIndex = Math.Clamp(slotIndex, 1, 10);
        return Path.Combine(SaveDirectoryPath, $"save_{safeSlotIndex:00}.json");
    }

    public static string ToGlobalPath(string path)
    {
        if (IsGodotVirtualPath(path))
        {
            return ProjectSettings.GlobalizePath(path);
        }

        return Path.GetFullPath(path);
    }

    public static void EnsureParentDirectoryExists(string path)
    {
        string globalPath = ToGlobalPath(path);
        string? directory = Path.GetDirectoryName(globalPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsGodotVirtualPath(string path)
    {
        return path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
    }
}
