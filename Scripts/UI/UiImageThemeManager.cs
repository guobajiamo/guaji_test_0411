using Godot;
using System;
using System.Collections.Generic;
using Test00_0410.Core.Helpers;

namespace Test00_0410.UI;

/// <summary>
/// UI 图片主题管理器。
/// 作用是读取 YAML 里的图片路径和颜色配置，再统一应用到主菜单和事件按钮上。
/// </summary>
public static class UiImageThemeManager
{
    private const string ConfigPath = "res://Configs/UI/ui_image_theme.yaml";
    private const string ConfigPathLegacy = "res://Configs/UI/ui_image_theme_legacy.yaml";
    private const string ConfigPathStitch = "res://Configs/UI/ui_image_theme_stitch.yaml";

    private static UiImageThemeConfig? _cachedConfig;
    private static string _themeMode = GameplayUiConfigLoader.UiThemeModeStitch;
    private static readonly Dictionary<ButtonStyleCacheKey, StyleBox> _buttonStyleCache = new();
    private static readonly Dictionary<string, Texture2D?> _textureCache = new(StringComparer.Ordinal);

    private readonly record struct ButtonStyleCacheKey(
        string ThemeMode,
        string ImagePath,
        Color FallbackColor,
        Color BorderColor);

    public static UiImageThemeConfig GetTheme()
    {
        _cachedConfig ??= LoadTheme();
        return _cachedConfig;
    }

    public static void ResetCache()
    {
        _cachedConfig = null;
        _buttonStyleCache.Clear();
        _textureCache.Clear();
    }

    public static void SetThemeMode(string? mode)
    {
        string normalizedMode = GameplayUiConfigLoader.NormalizeUiThemeMode(mode);
        if (string.Equals(_themeMode, normalizedMode, StringComparison.Ordinal))
        {
            return;
        }

        _themeMode = normalizedMode;
        ResetCache();
    }

    public static string GetThemeMode()
    {
        return _themeMode;
    }

    public static Control CreateMenuBackground()
    {
        UiImageThemeConfig config = GetTheme();

        Control root = new()
        {
            Name = "MenuBackgroundLayer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        ColorRect colorRect = new()
        {
            Name = "MenuBackgroundColor",
            Color = config.MenuBackgroundColor,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        colorRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(colorRect);

        Texture2D? backgroundTexture = LoadTexture(config.MenuBackgroundImagePath);
        if (backgroundTexture != null)
        {
            TextureRect textureRect = new()
            {
                Name = "MenuBackgroundImage",
                Texture = backgroundTexture,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(textureRect);
        }

        return root;
    }

    public static void ApplyButtonStyle(Button button, string styleKey)
    {
        UiButtonImageStyle style = GetTheme().GetButtonStyle(styleKey);

        button.AddThemeColorOverride("font_color", style.TextColor);
        button.AddThemeColorOverride("font_hover_color", style.TextColor);
        button.AddThemeColorOverride("font_pressed_color", style.TextColor);
        button.AddThemeColorOverride("font_disabled_color", style.DisabledTextColor);

        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(style.NormalImagePath, style.NormalColor, style.BorderColor));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(style.HoverImagePath, style.HoverColor, style.BorderColor));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(style.PressedImagePath, style.PressedColor, style.BorderColor));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(style.DisabledImagePath, style.DisabledColor, style.BorderColor));
    }

    private static UiImageThemeConfig LoadTheme()
    {
        YamlConfigLoader loader = new();
        Dictionary<string, object?> root = loader.LoadMap(ResolveConfigPath());

        UiImageThemeConfig config = new();

        if (root.TryGetValue("menu_background", out object? menuBackgroundValue)
            && menuBackgroundValue is Dictionary<string, object?> menuBackgroundMap)
        {
            config.MenuBackgroundColor = ParseColor(menuBackgroundMap, "fallback_color", new Color("11161d"));
            config.MenuBackgroundImagePath = GetString(menuBackgroundMap, "image_path");
        }

        if (root.TryGetValue("buttons", out object? buttonsValue)
            && buttonsValue is Dictionary<string, object?> buttonsMap)
        {
            foreach ((string styleKey, object? styleValue) in buttonsMap)
            {
                if (styleValue is not Dictionary<string, object?> styleMap)
                {
                    continue;
                }

                config.ButtonStyles[styleKey] = new UiButtonImageStyle
                {
                    NormalImagePath = GetString(styleMap, "normal_image"),
                    HoverImagePath = GetString(styleMap, "hover_image"),
                    PressedImagePath = GetString(styleMap, "pressed_image"),
                    DisabledImagePath = GetString(styleMap, "disabled_image"),
                    NormalColor = ParseColor(styleMap, "fallback_normal_color", new Color("2b3440")),
                    HoverColor = ParseColor(styleMap, "fallback_hover_color", new Color("3a4554")),
                    PressedColor = ParseColor(styleMap, "fallback_pressed_color", new Color("1c232c")),
                    DisabledColor = ParseColor(styleMap, "fallback_disabled_color", new Color("5c6470")),
                    TextColor = ParseColor(styleMap, "text_color", new Color("f3f4f6")),
                    DisabledTextColor = ParseColor(styleMap, "disabled_text_color", new Color("d1d5db")),
                    BorderColor = ParseColor(styleMap, "border_color", new Color("0b1016"))
                };
            }
        }

        return config;
    }

    private static string ResolveConfigPath()
    {
        if (string.Equals(_themeMode, GameplayUiConfigLoader.UiThemeModeLegacy, StringComparison.Ordinal))
        {
            return FileAccess.FileExists(ConfigPathLegacy)
                ? ConfigPathLegacy
                : ConfigPath;
        }

        return FileAccess.FileExists(ConfigPathStitch)
            ? ConfigPathStitch
            : ConfigPath;
    }

    private static StyleBox CreateButtonStyle(string imagePath, Color fallbackColor, Color borderColor)
    {
        string normalizedImagePath = imagePath?.Trim() ?? string.Empty;
        ButtonStyleCacheKey cacheKey = new(
            _themeMode,
            normalizedImagePath,
            fallbackColor,
            borderColor);
        if (_buttonStyleCache.TryGetValue(cacheKey, out StyleBox? cachedStyle))
        {
            return cachedStyle;
        }

        Texture2D? texture = LoadTexture(normalizedImagePath);
        if (texture != null)
        {
            StyleBoxTexture textureStyle = new()
            {
                Texture = texture
            };
            _buttonStyleCache[cacheKey] = textureStyle;
            return textureStyle;
        }

        StyleBoxFlat flatStyle = new()
        {
            BgColor = fallbackColor,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ShadowColor = new Color(0, 0, 0, 0.24f),
            ShadowSize = 6,
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8
        };
        _buttonStyleCache[cacheKey] = flatStyle;
        return flatStyle;
    }

    private static Texture2D? LoadTexture(string path)
    {
        string normalizedPath = path?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        if (_textureCache.TryGetValue(normalizedPath, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        if (!ResourceLoader.Exists(normalizedPath))
        {
            _textureCache[normalizedPath] = null;
            return null;
        }

        Texture2D? loadedTexture = ResourceLoader.Load<Texture2D>(normalizedPath);
        _textureCache[normalizedPath] = loadedTexture;
        return loadedTexture;
    }

    private static string GetString(Dictionary<string, object?> map, string key)
    {
        return map.TryGetValue(key, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static Color ParseColor(Dictionary<string, object?> map, string key, Color fallbackColor)
    {
        string text = GetString(map, key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallbackColor;
        }

        try
        {
            return new Color(text);
        }
        catch (Exception)
        {
            return fallbackColor;
        }
    }
}

/// <summary>
/// 整体 UI 图片主题配置。
/// </summary>
public sealed class UiImageThemeConfig
{
    public Color MenuBackgroundColor { get; set; } = new("11161d");

    public string MenuBackgroundImagePath { get; set; } = string.Empty;

    public Dictionary<string, UiButtonImageStyle> ButtonStyles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public UiButtonImageStyle GetButtonStyle(string styleKey)
    {
        return ButtonStyles.TryGetValue(styleKey, out UiButtonImageStyle? style)
            ? style
            : UiButtonImageStyle.CreateFallback();
    }
}

/// <summary>
/// 单组按钮样式定义。
/// 每种按钮都允许配置 4 张状态图，也允许只写颜色先跑通。
/// </summary>
public sealed class UiButtonImageStyle
{
    public string NormalImagePath { get; set; } = string.Empty;

    public string HoverImagePath { get; set; } = string.Empty;

    public string PressedImagePath { get; set; } = string.Empty;

    public string DisabledImagePath { get; set; } = string.Empty;

    public Color NormalColor { get; set; } = new("2b3440");

    public Color HoverColor { get; set; } = new("3a4554");

    public Color PressedColor { get; set; } = new("1c232c");

    public Color DisabledColor { get; set; } = new("5c6470");

    public Color TextColor { get; set; } = new("f3f4f6");

    public Color DisabledTextColor { get; set; } = new("d1d5db");

    public Color BorderColor { get; set; } = new("0b1016");

    public static UiButtonImageStyle CreateFallback()
    {
        return new UiButtonImageStyle();
    }
}
