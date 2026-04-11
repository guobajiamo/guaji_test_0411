using System.Collections.Generic;
using System.IO;
using Godot;
using Test00_0410.Core.Registry;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 树形结构打印工具。
/// 方便你在开发期快速看到分类和物品的层级是否正确。
/// </summary>
public static class TreePrinter
{
    public static void PrintToGodotConsole(ItemRegistry itemRegistry)
    {
        foreach (string line in BuildIndentedLines(itemRegistry))
        {
            GD.Print(line);
        }
    }

    public static void WriteToFile(ItemRegistry itemRegistry, string filePath)
    {
        RuntimePathHelper.EnsureParentDirectoryExists(filePath);
        string globalPath = RuntimePathHelper.ToGlobalPath(filePath);
        File.WriteAllLines(globalPath, BuildIndentedLines(itemRegistry));
    }

    public static List<string> BuildIndentedLines(ItemRegistry itemRegistry)
    {
        List<string> lines = new();
        AppendChildren(itemRegistry, string.Empty, 0, lines);
        return lines;
    }

    private static void AppendChildren(ItemRegistry itemRegistry, string parentId, int depth, List<string> lines)
    {
        foreach (var child in itemRegistry.GetChildren(parentId))
        {
            string indent = new string(' ', depth * 2);
            lines.Add($"{indent}- {child.Id} ({child.NameKey})");
            AppendChildren(itemRegistry, child.Id, depth + 1, lines);
        }
    }
}
