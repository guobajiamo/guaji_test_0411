using System.Collections.Generic;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 多语言管理器。
/// 前期即使不加载翻译表，也会把 key 自己当作显示文字返回。
/// </summary>
public class LocalizationManager
{
    private readonly Dictionary<string, string> _translations = new();

    public string CurrentLocale { get; private set; } = "zh";

    public void SetLocale(string locale)
    {
        CurrentLocale = locale;
    }

    public void LoadTranslations(Dictionary<string, string> translations)
    {
        _translations.Clear();

        foreach (KeyValuePair<string, string> pair in translations)
        {
            _translations[pair.Key] = pair.Value;
        }
    }

    public string Translate(string key)
    {
        return _translations.TryGetValue(key, out string? translated) ? translated : key;
    }
}
