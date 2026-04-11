using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Registry;

/// <summary>
/// 事件注册表。
/// 按事件 ID 和事件类型提供查询。
/// </summary>
public class EventRegistry
{
    private readonly Dictionary<string, EventDefinition> _events = new();

    public IReadOnlyDictionary<string, EventDefinition> Events => _events;

    public void LoadDefinitions(IEnumerable<EventDefinition> definitions)
    {
        _events.Clear();

        foreach (EventDefinition definition in definitions)
        {
            _events[definition.Id] = definition;
        }
    }

    public EventDefinition? GetEvent(string id)
    {
        return _events.GetValueOrDefault(id);
    }

    public List<EventDefinition> GetEventsByType(EventType eventType)
    {
        return _events.Values.Where(definition => definition.Type == eventType).ToList();
    }

    public List<EventDefinition> GetEventsByGroup(ButtonListGroup group)
    {
        return _events.Values
            .Where(definition => definition.ButtonListGroup == group)
            .OrderBy(definition => definition.Id)
            .ToList();
    }
}
