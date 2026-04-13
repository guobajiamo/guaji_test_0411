using System.Collections.Generic;
using Godot;
using Test00_0410.Core.Definitions;

namespace Test00_0410.Core.Registry;

/// <summary>
/// 区域注册表。
/// 负责集中保存所有区域定义。
/// </summary>
public class ZoneRegistry
{
    private readonly Dictionary<string, ZoneDefinition> _zones = new();

    public IReadOnlyDictionary<string, ZoneDefinition> Zones => _zones;

    public void LoadDefinitions(IEnumerable<ZoneDefinition> definitions)
    {
        _zones.Clear();

        foreach (ZoneDefinition definition in definitions)
        {
            if (_zones.ContainsKey(definition.Id))
            {
                GD.PushWarning($"[ZoneRegistry] 检测到重复 Zone ID：{definition.Id}。已保留先加载的定义，忽略来源 {definition.SourceFilePath}。");
                continue;
            }

            _zones[definition.Id] = definition;
        }
    }

    public ZoneDefinition? GetZone(string id)
    {
        return _zones.GetValueOrDefault(id);
    }
}
