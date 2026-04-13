# 36_Windows版手动正规打包说明文档

## 1. 先说结论

这个项目现在要想稳定打包出可运行的 Windows 版本，最稳的流程是：

1. 先准备好 Godot 4.6.2 Mono、Windows export templates、.NET SDK。
2. 先用 `dotnet publish` 生成 **正确的 C# 自包含运行时目录**。
3. 再用 Godot 导出 `exe/pck`。
4. 最终把下面 4 部分一起放进同一个目录：

- `test_00_0410.exe`
- `test_00_0410.console.exe`
- `test_00_0410.pck`
- `data_test_00_0410_windows_x86_64/`

如果只导出了 `exe + pck`，但没有正确的 `data_test_00_0410_windows_x86_64`，程序大概率会启动后立刻崩溃。

---

## 2. 这次排查后确认的关键经验

这次真正导致导出包崩溃的，不是游戏脚本本身，而是 **C# 运行时目录不正确**。

错误日志特征通常是：

```text
hostfxr_initialize_for_dotnet_command_line failed
Parameter "load_assembly_and_get_function_pointer" is null
Parameter "godot_plugins_initialize" is null
```

这说明：

- 导出包里虽然有 `exe`
- 但 `data_test_00_0410_windows_x86_64` 里的 .NET 运行时文件不是正确的 self-contained 发布版本
- 或者该目录缺文件
- 或者里面的 `deps.json / runtimeconfig.json` 被普通 Debug 构建版本覆盖了

这类错误和“游戏代码里哪个变量空了”不是一回事。

---

## 3. 打包前的准备条件

你本机至少要准备好下面这些东西：

### 3.1 Godot

- 版本：`Godot 4.6.2 Mono`
- 必须安装 `Windows export templates`

### 3.2 .NET

- 建议安装可用的 `.NET SDK`
- 当前项目目标框架是 `net8.0`
- 本机实际也可以用更高版本 SDK 驱动构建，但目标仍然是 `net8.0`

### 3.3 项目内已有的导出预设

当前项目已经有：

- 文件：`export_presets.cfg`
- 预设名：`Windows Desktop`

所以你不用重新手写导出预设，直接用现成的即可。

---

## 4. 最推荐的手动正规打包流程

下面这套是当前项目已经实测通过的流程。

### 4.1 第一步：进入项目根目录

项目根目录就是：

```text
l:\00_godot_project_0410\test-00-0410
```

### 4.2 第二步：先生成 C# 自包含运行时目录

在 PowerShell 中执行：

```powershell
dotnet publish .\test_00_0410.csproj `
  -c ExportDebug `
  -r win-x64 `
  --self-contained true `
  --ignore-failed-sources `
  -p:GodotTargetPlatform=windows `
  -o .\Build\data_test_00_0410_windows_x86_64
```

这一步的作用是：

- 生成 `test_00_0410.dll`
- 生成正确的 `test_00_0410.deps.json`
- 生成正确的 `test_00_0410.runtimeconfig.json`
- 生成 `hostfxr.dll`、`hostpolicy.dll`、`coreclr.dll` 等 .NET 运行时文件

### 4.3 第三步：再导出 Godot 的 exe 和 pck

如果你想用命令行导出，可以执行：

```powershell
& 'L:\new_workspace_0410\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe' `
  --headless `
  --path 'l:\00_godot_project_0410\test-00-0410' `
  --export-debug 'Windows Desktop' `
  'l:\00_godot_project_0410\test-00-0410\Build\test_00_0410.exe' `
  --verbose `
  --log-file 'l:\00_godot_project_0410\test-00-0410\godot_build.log'
```

这一步会生成：

- `Build\test_00_0410.exe`
- `Build\test_00_0410.console.exe`
- `Build\test_00_0410.pck`

### 4.4 第四步：确认最终目录结构完整

`Build` 目录下最终应该至少有：

```text
Build/
  test_00_0410.exe
  test_00_0410.console.exe
  test_00_0410.pck
  data_test_00_0410_windows_x86_64/
```

缺任何一个，都不算打包完成。

---

## 5. 如果你更想在 Godot 编辑器里手动点按钮打包

也可以用 Godot 编辑器操作：

1. 用 `Godot 4.6.2 Mono` 打开项目。
2. 确认 `Project -> Export` 中存在 `Windows Desktop` 预设。
3. 先手动执行上面的 `dotnet publish` 命令，确保 `Build\data_test_00_0410_windows_x86_64` 正常生成。
4. 再在 Godot 里点击 `Export Project`，导出到 `Build\test_00_0410.exe`。

注意：

- 这个项目是 C# 项目
- Godot 自己有时会尝试自动调用 `dotnet publish`
- 但如果环境里 NuGet 缓存、签名校验、网络源状态不理想，就可能出现“Godot 看起来导出了，但运行时目录不完整”的情况

所以对于这个项目，**先手动 `dotnet publish`，再执行 Godot 导出** 是最稳的。

---

## 6. 当前项目里最容易踩的坑

### 6.1 不要把普通 Debug 构建产物误当成导出运行时目录

下面这个目录里的文件：

```text
.godot\mono\temp\bin\Debug
```

只能算普通调试构建结果，不能直接拿来替代：

```text
Build\data_test_00_0410_windows_x86_64
```

原因是：

- 普通 Debug 的 `deps.json`
- 普通 Debug 的 `runtimeconfig.json`
- self-contained 发布版的 `deps.json`
- self-contained 发布版的 `runtimeconfig.json`

它们不是一回事。

如果你把普通 Debug 版本覆盖进导出包，程序就可能出现：

```text
hostfxr_initialize_for_dotnet_command_line failed
```

### 6.2 不要随便把 `NUGET_PACKAGES` 指向项目内不完整缓存

这次排查里已经确认：

- 使用系统默认缓存 `C:\Users\AA\.nuget\packages` 时，`dotnet publish` 可以正常成功
- 强行把 `NUGET_PACKAGES` 指到项目里的 `.nuget\packages`，如果里面缺少 `win-x64` 运行时包，就会失败

因此你自己手动打包时，最简单的做法是：

- 不额外设置 `NUGET_PACKAGES`
- 直接让 `dotnet` 使用系统默认 NuGet 缓存

### 6.3 如果看到程序能进主菜单，但点击按钮没反应

这属于另一类问题。

本项目之前出现过：

- 编辑器里剧本资源是 `.tres`
- 导出包里目录可见的是 `.tres.remap`
- 旧逻辑只扫描 `.tres`
- 结果导出包里剧本数量被识别成 `0`

现在这个问题已经修过了，导出包启动日志里应当看到：

```text
已发现剧本数量：2
```

如果你以后又遇到主菜单按钮点了没反应，第一件事就是看日志里剧本数量是不是 `0`。

---

## 7. 推荐的打包后验证方法

### 7.1 最简单的验证

直接双击运行：

```text
test_00_0410.exe
```

看是否能：

- 正常进入主菜单
- 点击“新的游戏”进入主界面
- 点击“进入游戏测试”进入测试剧本

### 7.2 更适合排错的验证

如果想看导出包的运行日志，用：

```powershell
.\Build\test_00_0410.console.exe --headless --quit-after 5 --verbose --log-file .\Build\export_runtime_check.log
```

如果日志里看到下面这些内容，就说明导出结构基本正常：

```text
.NET: hostfxr initialized
.NET: GodotPlugins initialized
已发现剧本数量：2
游戏主菜单入口已准备完成
```

---

## 8. 一套适合你以后重复使用的完整命令模板

下面这套可以当成你以后自己打包时的模板。

### 8.1 清理旧 Build

```powershell
Remove-Item -Recurse -Force .\Build\* -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path .\Build | Out-Null
```

### 8.2 生成 C# 自包含运行时

```powershell
dotnet publish .\test_00_0410.csproj `
  -c ExportDebug `
  -r win-x64 `
  --self-contained true `
  --ignore-failed-sources `
  -p:GodotTargetPlatform=windows `
  -o .\Build\data_test_00_0410_windows_x86_64
```

### 8.3 导出 exe 和 pck

```powershell
& 'L:\new_workspace_0410\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe' `
  --headless `
  --path 'l:\00_godot_project_0410\test-00-0410' `
  --export-debug 'Windows Desktop' `
  'l:\00_godot_project_0410\test-00-0410\Build\test_00_0410.exe' `
  --verbose `
  --log-file 'l:\00_godot_project_0410\test-00-0410\godot_build.log'
```

### 8.4 导出后自检

```powershell
Get-ChildItem .\Build
```

确认有：

- `test_00_0410.exe`
- `test_00_0410.console.exe`
- `test_00_0410.pck`
- `data_test_00_0410_windows_x86_64`

---

## 9. 如果你想打正式版 Release

这次实际验证通过的是 `Debug` 导出链。

如果你以后想做正式发布版，可以把两步里的配置同时改成 Release 语义：

### 9.1 dotnet publish

```powershell
dotnet publish .\test_00_0410.csproj `
  -c ExportRelease `
  -r win-x64 `
  --self-contained true `
  --ignore-failed-sources `
  -p:GodotTargetPlatform=windows `
  -o .\Build\data_test_00_0410_windows_x86_64
```

### 9.2 Godot 导出

```powershell
--export-release 'Windows Desktop'
```

但是否完全适合你当前项目的发布使用，建议你到时再实际测一轮。

---

## 10. 最后给你的实际建议

对于这个项目，后续你自己打包时，建议固定遵守下面这条流程：

1. 先清空 `Build`
2. 先执行 `dotnet publish`
3. 再执行 Godot 导出
4. 再检查四件套是否完整
5. 最后双击 `exe` 实机验证

这样最不容易出现“看起来打包成功，实际上运行时目录坏了”的隐性问题。

如果你以后又遇到打包异常，优先检查这三类日志：

- `godot_build.log`
- 导出包运行日志 `godot.log`
- `export_runtime_check.log`

这样通常很快就能判断问题是在：

- Godot 导出阶段
- .NET 运行时阶段
- 还是游戏逻辑阶段

