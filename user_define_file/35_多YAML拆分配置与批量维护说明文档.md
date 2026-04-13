# 35_多YAML拆分配置与批量维护说明文档

## 1. 先说最重要的结论

现在这个项目里，剧本配置已经从“每种类型只允许一个 YAML 文件”升级成了：

- 既支持单文件加载
- 也支持目录式多 YAML Bundle 加载

也就是说，以后你在 `.tres` 里给某个配置项填写的路径，可以是：

- 一个具体的 `.yaml` 文件
- 一个目录

如果填写的是目录，程序会：

1. 递归扫描该目录下的全部 `.yaml`
2. 按文件名排序
3. 将同类型数据合并后再当成“一个逻辑文件”继续处理

所以从你的使用感受上看：

- 你自己可以把数据拆成很多小文件方便管理
- 程序读取时仍然会把它们视为同一份完整配置

这正是这次改动的核心目标。

## 2. 当前剧本资源的读取方式已经改成什么样

### 2.1 测试剧本

文件：

- `Resources/Scenarios/test_game_scenario.tres`

它现在已经改成指向目录，而不是单个 YAML：

- `CategoriesConfigPath = "res://Configs/Scenarios/TestGame/Items/Categories"`
- `ItemsConfigPath = "res://Configs/Scenarios/TestGame/Items/Definitions"`
- `SkillsConfigPath = "res://Configs/Scenarios/TestGame/Skills/Definitions"`
- `OneshotEventsConfigPath = "res://Configs/Scenarios/TestGame/Events/Oneshot"`
- `ClickEventsConfigPath = "res://Configs/Scenarios/TestGame/Events/Click"`
- `IdleEventsConfigPath = "res://Configs/Scenarios/TestGame/Events/Idle"`
- `FactionsConfigPath = "res://Configs/Scenarios/TestGame/Factions/Definitions"`
- `ZonesConfigPath = "res://Configs/Scenarios/TestGame/Zones/Definitions"`
- `LocalizationPath = "res://Configs/Scenarios/TestGame/Localization/Bundle"`
- `InfoPanelConfigPath = "res://Configs/Scenarios/TestGame/UI/InfoPanel"`

### 2.2 正式主线剧本

文件：

- `Resources/Scenarios/main_story_scenario.tres`

它现在也已经切到目录式路径：

- `CategoriesConfigPath = "res://Configs/Scenarios/MainStory/Items/Categories"`
- `ItemsConfigPath = "res://Configs/Scenarios/MainStory/Items/Definitions"`
- `SkillsConfigPath = "res://Configs/Scenarios/MainStory/Skills/Definitions"`
- `OneshotEventsConfigPath = "res://Configs/Scenarios/MainStory/Events/Oneshot"`
- `ClickEventsConfigPath = "res://Configs/Scenarios/MainStory/Events/Click"`
- `IdleEventsConfigPath = "res://Configs/Scenarios/MainStory/Events/Idle"`
- `FactionsConfigPath = "res://Configs/Scenarios/MainStory/Factions/Definitions"`
- `ZonesConfigPath = "res://Configs/Scenarios/MainStory/Zones/Definitions"`
- `LocalizationPath = "res://Configs/Scenarios/MainStory/Localization/Bundle"`
- `InfoPanelConfigPath = "res://Configs/Scenarios/MainStory/UI/InfoPanel"`

## 3. 程序到底如何合并多 YAML

### 3.1 列表型配置

以下这些配置本质上都是“列表型实例数据”：

- `categories`
- `items`
- `skills`
- `events`
- `factions`
- `zones`

当你把它们拆成多个 YAML 后，程序会：

1. 按文件名顺序读取
2. 取出每个文件里对应根键下的列表
3. 直接拼接成一整份大列表

例如：

```text
Items/Definitions/
  00_Gathering_Items.yaml
  01_HumanVillage_Items.yaml
  02_Tool_Items.yaml
  03_Fishing_Items.yaml
```

程序最终看到的效果，相当于把这 4 份文件里的 `items:` 全部接在一起。

### 3.2 Map 型配置

以下这些配置更偏“字典 / 面板配置 / 文本配置”：

- `Localization`
- `InfoPanel`
- `GameplayLayout`

它们采用的是“递归合并”规则：

- 字典对字典：按键合并
- 列表对列表：追加
- 标量对标量：后加载文件覆盖先加载文件

这很适合这种场景：

- `00_Settings.yaml` 放全局参数
- `01_*.yaml`、`02_*.yaml` 放分模块内容

## 4. 文件顺序规则非常重要

多 YAML 模式下，文件名顺序本身就是配置的一部分。

推荐你始终使用：

```text
00_
01_
02_
03_
```

这种前缀。

原因如下：

- 它能明确控制加载顺序
- 它能让同目录内容更容易人工查找
- 它能让同 `definition_order` 的数据仍然保持稳定顺序

## 5. 现在项目里已经给你的拆分样例

### 5.1 道具

测试剧本里已经拆成了：

```text
Configs/Scenarios/TestGame/Items/
  Categories/
    00_TestGame_Categories.yaml
  Definitions/
    00_Gathering_Items.yaml
    01_HumanVillage_Items.yaml
    02_Tool_Items.yaml
    03_Fishing_Items.yaml
```

你以后完全可以继续追加：

- `04_Combat_Items.yaml`
- `05_OldHell_Items.yaml`
- `06_BambooForest_Items.yaml`

### 5.2 技能

测试剧本里已经拆成了：

```text
Configs/Scenarios/TestGame/Skills/Definitions/
  00_Chopping_Skill.yaml
  01_Fishing_Skill.yaml
```

### 5.3 三类事件

现在三类事件已经放进了独立子目录，这正是你希望的结构。

```text
Configs/Scenarios/TestGame/Events/
  Oneshot/
    01_HumanVillage_Oneshot.yaml
    02_YoukaiMountain_Oneshot.yaml
  Click/
    01_MagicForest_Click.yaml
    02_HumanVillage_Click.yaml
    03_YoukaiMountain_Click.yaml
  Idle/
    01_MagicForest_Idle.yaml
    02_YoukaiMountain_Idle.yaml
```

这意味着以后你继续扩内容时，不会再把三种事件类型都挤在同一个目录里。

### 5.4 势力与 NPC

测试剧本里已经拆成：

```text
Configs/Scenarios/TestGame/Factions/Definitions/
  01_ScarletMansion_Factions.yaml
  02_YoukaiMountain_Factions.yaml
  03_Eientei_Factions.yaml
```

每个文件里都可以同时带：

- `factions:`
- `npcs:`

程序会把它们一起合并。

### 5.5 Zone

测试剧本里已经拆成：

```text
Configs/Scenarios/TestGame/Zones/Definitions/
  01_MagicForest_Zones.yaml
  02_MistyLake_Zones.yaml
  03_YoukaiTrail_Zones.yaml
```

### 5.6 右上角信息面板

测试剧本里已经拆成：

```text
Configs/Scenarios/TestGame/UI/InfoPanel/
  00_Summary.yaml
  01_NoScenario_And_Story.yaml
  02_TestScenario.yaml
```

这是 Map 合并模式的示例：

- `00_Summary.yaml` 放 `summary_lines`
- `01_*.yaml`、`02_*.yaml` 放不同状态下的 `states`

### 5.7 新 UI 的 GameplayLayout

测试剧本里已经按一级区域拆分了：

```text
Configs/Scenarios/TestGame/UI/GameplayLayout/
  00_Settings.yaml
  01_HumanVillage_Zone.yaml
  02_MagicForest_Zone.yaml
  03_Hakurei_Zone.yaml
  04_YoukaiMountain_Zone.yaml
  05_ScarletMansion_Zone.yaml
  06_OldHell_Zone.yaml
  07_BambooForest_Zone.yaml
```

这就是你后续最推荐采用的写法：

- `00_Settings.yaml` 只放全局设置，例如 `default_area_id`
- 每个一级区域单独一个 YAML 文件

## 6. 以后新增内容时，最推荐的目录组织方式

### 6.1 道具

推荐：

```text
Items/Definitions/
  00_Shared_Items.yaml
  01_HumanVillage_Items.yaml
  02_MagicForest_Items.yaml
  03_Combat_Items.yaml
  04_OldHell_Items.yaml
```

### 6.2 事件

推荐：

```text
Events/
  Oneshot/
    01_HumanVillage_Oneshot.yaml
    02_MagicForest_Oneshot.yaml
  Click/
    01_HumanVillage_Click.yaml
    02_MagicForest_Click.yaml
  Idle/
    01_HumanVillage_Idle.yaml
    02_MagicForest_Idle.yaml
```

### 6.3 GameplayLayout

推荐：

```text
UI/GameplayLayout/
  00_Settings.yaml
  01_HumanVillage_Zone.yaml
  02_MagicForest_Zone.yaml
  03_Hakurei_Zone.yaml
```

这会让你查某个一级区域时，只打开对应那一个文件就够了。

## 7. 道具在多文件模式下如何排序

这是你特别提到的重点，现在已经补了稳定排序方案。

当前道具树排序规则是：

1. `definition_order`
2. 所在文件顺序
3. 文件内定义顺序
4. `id`

这意味着：

- 如果两个道具 `definition_order` 一样
- 它们仍然会按照文件名前缀顺序稳定排列

所以以后推荐你这样理解：

- `definition_order` 决定大排序
- 文件名前缀决定分包顺序
- 文件内位置决定同文件内微调顺序

这样即使拆成很多 YAML，也不会让背包排序随机飘动。

## 8. 新 UI 的一级区域现在应该怎么拆

### 8.1 规则

最推荐的规则是：

- 一个一级区域一个文件
- 这个文件里同时写它的全部二级区域
- 每个二级区域里再写自己的全部子场景

也就是：

```yaml
regions:
  - id: "human_village"
    title: "人间之里"
    areas:
      - id: "market_street"
        title: "商店街"
        scenes:
          - id: "dessert_house"
            title: "琉璃点心屋"
            event_ids: []
```

### 8.2 为什么这样拆最好

因为这样有几个好处：

- 查“人间之里”时只需要打开 `01_HumanVillage_Zone.yaml`
- 新增“人间之里”的二级区域时不会碰到别的一级区域
- 新增“人间之里”的子场景时不会在一个超长总文件里迷路

## 9. 子场景与事件绑定现在应该怎么理解

这一点和旧写法一样重要，但现在更适合拆文件维护。

### 9.1 事件 YAML 负责定义事件本身

例如：

- 名称
- 说明
- 前置显示条件
- 前置互动条件
- 后置隐藏条件
- 消耗
- 效果

### 9.2 GameplayLayout 负责定义事件出现在哪个子场景里

例如：

```yaml
event_ids:
  - "evt_pickup_stone_axe"
  - "evt_read_chopping_notice"
  - "evt_stranger_reward_offer"
```

这表示这些按钮只会出现在该子场景卡片里。

所以以后你新增一个事件时，通常要完成两步：

1. 在事件目录里新增事件定义
2. 在对应子场景的 `event_ids` 里挂上该事件 ID

缺一步都不行。

## 10. InfoPanel 和 GameplayLayout 的自动关系

当前项目仍然没有单独的 `GameplayLayoutPath` 字段。

程序规则是：

- 如果 `InfoPanelConfigPath` 指向的是单个文件
  - 则自动去同目录找 `gameplay_layout.yaml`
- 如果 `InfoPanelConfigPath` 指向的是目录
  - 则自动去同级目录下找 `GameplayLayout/`

也就是说，现在这种结构是成套使用的：

```text
UI/
  InfoPanel/
    ...
  GameplayLayout/
    ...
```

所以你以后新建剧本时，最好继续保持这个目录关系。

## 11. 多 YAML 模式下现在新增了哪些警告与保护

这次我额外补了很多你没有明说、但实际维护时非常有用的保护。

### 11.1 重复 ID 警告

现在以下内容如果出现重复 ID，会打印警告：

- 分类
- 道具
- 技能
- 事件
- 势力
- NPC
- Zone

而且警告里会带来源文件信息，方便你定位重复定义来自哪两份 YAML。

### 11.2 GameplayLayout 重复绑定警告

现在如果同一个事件 ID 被你不小心挂到了两个子场景里，也会打印警告。

### 11.3 GameplayLayout 重复区域 ID 警告

现在如果你写出了重复的：

- 一级区域 ID
- 二级区域 ID
- 子场景 ID

也会打印警告。

### 11.4 条件数量不一致报错

以下这些数量字段如果和实际条目数不一致，会直接报错提示：

- 事件的 `display_condition_count`
- 事件的 `interaction_condition_count`
- 事件的 `hide_condition_count`
- 区域的 `visibility_condition_count`

这能帮你更快发现 YAML 手误。

## 12. 你最担心的“以后加新字段要改十几个文件”现在怎么解决

这次我给项目加了一个专门的辅助脚本：

- `Tools/ConfigTools/Backfill-YamlField.ps1`

它的目标不是替你理解业务，而是替你做“批量检查哪些实例缺了某字段”和“按默认值批量补上字段”。

### 12.1 它适合什么场景

例如你以后给 `items` 新增了一个字段：

- `deprecated`

你可以先做只读审查：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\ConfigTools\Backfill-YamlField.ps1 `
  -Path .\Configs\Scenarios\TestGame\Items\Definitions `
  -RootKey items `
  -FieldName deprecated
```

它会告诉你：

- 哪些文件里有多少个 item 缺这个字段
- 每个缺字段的 item 是哪一行、哪个 `id`

### 12.2 如果你确认要批量补默认值

例如你想统一补：

- `deprecated: false`

可以这样：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\ConfigTools\Backfill-YamlField.ps1 `
  -Path .\Configs\Scenarios\TestGame\Items\Definitions `
  -RootKey items `
  -FieldName deprecated `
  -DefaultValue false `
  -Apply
```

脚本会把字段插入到每个实例的 `id` 后面。

### 12.3 这个脚本当前最适合处理哪些类型

最适合处理这类“顶层就是实例列表”的 YAML：

- `categories`
- `items`
- `skills`
- `events`
- `factions`
- `zones`
- `regions`

也就是说，它特别适合解决你举的那个例子：

- “我突然要给一级区域新增一个字段”

因为现在一级区域本来就已经拆成了“一文件一个一级区域”。

## 13. 为什么我没有强行删除旧单文件

旧的单文件我目前保留了，原因有三点：

- 方便你对照旧写法和新写法
- 万一你临时想回查历史配置，不会直接找不到源文件
- 避免一次性重构时把你已有注释和参考材料直接打散

也就是说：

- 运行时现在走的是新的目录式路径
- 旧文件仍然保留作参考

## 14. 以后新建一个剧本时最推荐的模板结构

```text
Configs/Scenarios/YourScenario/
  Items/
    Categories/
      00_YourScenario_Categories.yaml
    Definitions/
      00_Shared_Items.yaml
      01_RegionA_Items.yaml
  Skills/
    Definitions/
      00_Base_Skills.yaml
  Events/
    Oneshot/
      01_RegionA_Oneshot.yaml
    Click/
      01_RegionA_Click.yaml
    Idle/
      01_RegionA_Idle.yaml
  Factions/
    Definitions/
      01_RegionA_Factions.yaml
  Zones/
    Definitions/
      01_RegionA_Zones.yaml
  Localization/
    Bundle/
      00_zh.yaml
  UI/
    InfoPanel/
      00_Summary.yaml
      01_States.yaml
    GameplayLayout/
      00_Settings.yaml
      01_RegionA_Zone.yaml
```

对应 `.tres` 就指向这些目录。

## 15. 这次额外帮你补上的、你原需求里没明确写但实际很有价值的点

### 15.1 兼容旧单文件

你不需要一次性把所有剧本都拆完。

因为现在：

- 老写法还能继续跑
- 新写法也能跑

### 15.2 正式剧本也切到了目录式接口

这样以后你给主线扩内容时，不会再出现：

- 测试剧本能拆
- 正式剧本还得走旧结构

### 15.3 合并后仍然保留来源顺序信息

这样运行时排序和警告提示都能知道：

- 这个定义来自哪一个文件
- 它在该文件里的顺位是什么

### 15.4 对可疑配置更容易定位

现在一旦出现重复 ID 或重复绑定，你更容易从日志里直接反推出：

- 是哪份 YAML 有问题
- 是哪一类定义冲突

## 16. 一句话记住这次新规则

以后同类型配置你可以放心拆成很多 YAML。

对你来说它们是很多小文件。

对程序来说，它们会在运行时先合并，再像一份完整配置那样继续处理。
