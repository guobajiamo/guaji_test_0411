using Godot;

namespace Test00_0410.UI;

/// <summary>
/// stitch_Element_UI 对应的通用样式库。
/// 当前用于弹窗底框、深色日志全屏底框与 Tooltip 底框。
/// </summary>
public static class StitchElementStyleLibrary
{
    public static StyleBoxFlat CreateLightDialogFrame(int cornerRadius = 18)
    {
        int radius = Mathf.Max(8, cornerRadius);
        return new StyleBoxFlat
        {
            BgColor = new Color("#fbf9f5"),
            BorderColor = new Color("#d9dbd2"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0, 0, 0, 0.14f),
            ShadowSize = 18,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12
        };
    }

    public static StyleBoxFlat CreateLightSubPanelFrame(int cornerRadius = 14)
    {
        int radius = Mathf.Max(6, cornerRadius);
        return new StyleBoxFlat
        {
            BgColor = new Color("#f5f4ef"),
            BorderColor = new Color("#e2e3db"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
        };
    }

    public static StyleBoxFlat CreateDeepOpaqueFrame(int cornerRadius = 18)
    {
        int radius = Mathf.Max(8, cornerRadius);
        return new StyleBoxFlat
        {
            BgColor = new Color("#224545"),
            BorderColor = new Color("#365858"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color("#142525") { A = 0.42f },
            ShadowSize = 16,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10
        };
    }

    public static StyleBoxFlat CreateDeepTooltipFrame(int cornerRadius = 12)
    {
        int radius = Mathf.Max(6, cornerRadius);
        return new StyleBoxFlat
        {
            BgColor = new Color("#224545"),
            BorderColor = new Color("#426464"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0, 0, 0, 0.32f),
            ShadowSize = 10,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
        };
    }
}
