# 57_stitch_UI重置与新旧主题切换接口说明_0415

## 1. 本轮目标
- 启动并落地第一阶段 UI 重置：在现有 2K + 自适应框架上接入 stitch 风格。
- 提供可复用的 UI 主题切换接口（新UI/旧UI），并在系统标签页提供一键切换按钮。
- 默认进入新UI(stitch)，保留旧UI(legacy)用于调试与回归。

## 2. 已完成改动

### 2.1 UI主题模式基础设施
- 新增并统一主题模式常量：`legacy`、`stitch`。
- `GameplayUiConfigLoader.LoadTheme(...)` 支持按模式加载：
  - `res://Configs/UI/gameplay_ui_theme_legacy.yaml`
  - `res://Configs/UI/gameplay_ui_theme_stitch.yaml`
- `UiImageThemeManager` 支持按模式加载：
  - `res://Configs/UI/ui_image_theme_legacy.yaml`
  - `res://Configs/UI/ui_image_theme_stitch.yaml`

### 2.2 存档与运行时状态
- `PlayerUiState` 新增 `UiThemeMode`，默认值为 `stitch`。
- `SaveManager` 已支持 `UiThemeMode` 的保存/读取，空值回退到 `stitch`。

### 2.3 游戏主界面主题切换流程
- `GamePlayUI` 新增主题切换能力：
  - `UseStitchUiTheme()`
  - `UseLegacyUiTheme()`
  - `SwitchUiThemeMode(string mode)`
- 切换时会：
  1) 写入玩家 UI 状态；
  2) 重新加载主题配置；
  3) 重建 UI 结构并刷新全部面板。

### 2.4 系统标签页切换入口
- `SystemPanel.Configure(...)` 扩展支持 UI 切换参数。
- 系统页新增按钮：
  - `使用新UI(stitch)`
  - `使用旧UI(legacy)`
- 当前激活主题按钮会禁用并标记，避免重复切换。

### 2.5 背包界面 stitch/legacy 分支收尾
- `InventoryPanel` 样式从硬编码单风格改为按主题模式分支：
  - 导航栏面板样式
  - 主区左右面板样式
  - 搜索/筛选标题颜色
  - 空列表提示色
  - 标签按钮激活/非激活文字色
  - 物品槽按钮样式（含 stitch 圆角轻色版本）
  - 选中槽位高亮色
- 新增内部主题判断与颜色辅助方法，确保切换主题后可正确重建外观。

## 3. 验证结果
- `dotnet build .\test_00_0410.csproj`：通过（0 error / 0 warning）。
- Headless UI 冒烟：`UiFeatureSmokeTest.tscn` 已运行，日志未见脚本报错。

## 4. 当前阶段说明
- 本轮已完成“主题切换机制 + 关键界面接入”的可运行版本，满足默认新UI并可一键切回旧UI调试。
- 后续可在同一接口下继续追加：
  - 白天/夜晚主题
  - 更完整的 stitch 资源贴图替换（按钮、边框、装饰件逐模块覆盖）。
