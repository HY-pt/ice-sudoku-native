# ice·sudoku 原生绿色版（WPF / WinUI Fluent）

<p align="center">
  <img src="native/Assets/home.png" width="360" alt="ice·sudoku 启动页图" />
</p>

<p align="center">Windows 原生离线数独 · Fluent / WinUI 外观 · 自包含单文件 · HY-pt 维护</p>

Windows 原生离线数独小游戏，以 WPF（.NET 10）重新实现，并按 WinUI Gallery 对齐 Fluent 外观：沉浸式标题栏、明暗与高对比度主题、横竖两种布局、草稿模式、逻辑提示。程序打包为自包含单文件 exe，解压双击即玩，无需安装、无需联网、无需浏览器。

> 本仓库 fork 自上游 ice·sudoku。`native-wpf` 分支（默认分支）为 Windows 原生绿色版；`master` 分支保留上游原样内容。

## 界面预览

启动页与游戏过程截图（浅色 / 深色、竖版 / 横版、草稿与提示条）：

<p align="center">
  <img src="docs/home_light.png" width="300" alt="启动页" />&nbsp;&nbsp;<img src="docs/game_light.png" width="300" alt="竖版对局" />
</p>
<p align="center"><sub>启动页 · 竖版对局</sub></p>

<p align="center">
  <img src="docs/draft_light.png" width="300" alt="草稿模式" />&nbsp;&nbsp;<img src="docs/hint_light.png" width="300" alt="提示条" />
</p>
<p align="center"><sub>草稿模式（候选小数字） · 提示条 InfoBar</sub></p>

<p align="center">
  <img src="docs/horizontal_light.png" width="380" alt="横版浅色" />&nbsp;&nbsp;<img src="docs/horizontal_dark.png" width="380" alt="横版深色" />
</p>
<p align="center"><sub>横版布局（浅色） · 横版布局（深色）</sub></p>

## 功能特性

**玩法**

- 生成唯一解题目，支持简单 / 中等 / 困难 / 专家四档难度
- 不做即时判错、不泄露答案；冲突仅标红可见重复，盘面填满且无冲突即完成
- 「重来」清空全部自填数字与草稿，每局 3 次机会
- 「提示」给出逻辑可推导的一步；「自动解」直接展示答案
- 选中预设格后按 1-9（键盘或数字盘），自动跳转到最近的同数字格并高亮全盘该数字，方便观察分布

**界面**

- 横竖两种布局：竖版棋盘在上、数字盘在下；横版棋盘在左、数字盘在右（3×3 紧凑键位、大号数字），标题栏按钮一键互换
- 统计框（用时 / 重来 / 难度）在两种布局下均顶部居中，暂停按钮位于右上角
- 草稿模式：空格记录候选小数字；棋盘所有数字（含预设）变铅笔灰；数字键大数字变淡，右上角出现加粗的正常色同数字角标，表示该数字仍可用
- 非草稿模式：数字键下方小数字为该数字剩余可填数量，填完自动置灰
- 明暗主题切换；Win11 圆角窗口；最大化精确铺满工作区
- 暂停页可调整难度，开始新游戏时生效

**WinUI 对齐**

- Segoe Fluent Icons 图标、双层键盘焦点框、按钮按下 / 悬停 / 禁用态、悬停 Tooltip
- InfoBar 提示条、统一字阶与 4px 间距、卡片阴影、切页与浮层过渡动画
- 棋盘格、数字键与图标按钮均有读屏名称；跟随系统高对比度主题
- 背景默认柔和纯色；`--mica` 在支持的设备上尝试 Windows 11 云母材质（不可用时自动保持纯色可读）

## 下载与安装

- 最新发布：[v2.6.2](https://github.com/HY-pt/ice-sudoku-native/releases/tag/v2.6.2)，资产 `ice_sudoku_v2.6.2_win-x64.exe`
- 系统要求：Windows 10 / 11 64 位
- 解压后双击运行，无需安装；首次运行若被 SmartScreen 拦截，选择「仍要运行」即可（本地自编译绿色软件，未签名）
- 可选启动参数：`--mica` 尝试云母材质，`--solid` 强制纯色背景

## 操作

| 动作 | 按键 |
| --- | --- |
| 选中格子 | 鼠标点击 / 方向键 |
| 填数 | 1-9 或数字键 |
| 聚焦同数字 | 选中预设格后按 1-9（键盘或数字盘），跳到最近的同数字格并高亮全盘 |
| 擦除 | 0 / Del / Backspace |
| 草稿模式 | N 或「✏ 草稿」 |
| 暂停 / 继续 | Esc 或暂停按钮 |
| 横竖布局 | 标题栏切换按钮 |
| 明暗主题 | 标题栏月亮 / 太阳 |

## 从源码构建

需要 .NET SDK 10（Windows）：

```powershell
dotnet publish native/ice_sudoku_wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

产物为自包含单文件 exe，默认输出到 `native/bin/Release/net10.0-windows/win-x64/publish/`。

## 项目结构

```text
native/                 WPF 原生版源码（.NET 10）
  Assets/               启动页插画与应用图标
  Themes/               明暗、高对比度主题与 Fluent 样式
  MainWindow.xaml       界面布局
  MainWindow.xaml.cs    交互、游戏流程与布局切换
  SudokuEngine.cs       题目生成、唯一解校验与逻辑求解
docs/                   README 截图
```

根目录其余文件为上游 ice·sudoku 的内容（Python / C 求解器、题库等），`master` 分支保持原样。

## 项目背景

上游 [tropicalwzc/ice_sudoku.github.io](https://github.com/tropicalwzc/ice_sudoku.github.io)（MIT 许可）是覆盖 iOS / macOS / Windows / Ubuntu 的数独程序，支持逻辑分步求解与 ResNet34 扫描识别；其 Windows 官方版止于 2.2.1。

本分支的由来：

1. **网页绿色版**：以上游项目为起点，先做了浏览器打开、双击即玩的离线网页绿色版（免安装、免联网），供日常使用。
2. **原生重写**：为贴近 Windows 原生体验（Fluent / WinUI 外观、沉浸式标题栏、明暗主题、单文件离线），用 WPF（.NET 10）完全重写。数独引擎（生成、唯一解校验、逻辑分步求解与提示）、界面与主题均为本分支独立实现，不含上游二进制或代码；延续其名称与玩法脉络，版本号接在上游 Windows 版之后（2.2.1 → 2.3 起）。

## 版本历史

| 版本 | 内容 |
| --- | --- |
| 2.6.2 | 选中预设格后按数字键（键盘或数字盘）即可聚焦该数字：自动跳转到最近的同数字格，并高亮全盘该数字；空格填数与草稿逻辑保持不变 |
| 2.6.1 | 启动页立绘重新抠图（保留眼睛高光，头发与轮廓边缘透明羽化，去除白边锯齿），应用图标同步更新；启动页底部标注版本、制作与维护者及 GitHub 仓库入口；横版数字盘改为紧凑 3×3 键位并放大键内数字，右侧按键区与棋盘上下齐平，暂停按钮放大为 48×48 圆角矩形；草稿大数字进一步淡化、可用角标加粗放大 |
| 2.6.0 | 按 WinUI Gallery 逐项对齐：Segoe Fluent Icons、双层焦点框、按下 / 禁用态、InfoBar 提示条、字阶与 4px 间距、卡片阴影与切页动效、读屏名称、高对比度主题；默认柔和纯色背景，`--mica` 可选云母材质 |
| 2.5.2 | 横版右侧面板与棋盘等高对齐、数字键改为近方形大按键 |
| 2.5.1 | 两种布局统计框顶部居中、暂停按钮右上角；草稿视觉重做（棋盘数字含预设全部变灰、数字键右上角同数字可用角标） |
| 2.5 | 标题栏横竖布局切换、草稿模式、三次错误改为三次重来、取消即时判错与答案泄露 |
| 2.4 | 启动页与应用图标、暂停页与难度调整、沉浸式标题栏、Win11 圆角与工作区最大化、启动图 / 图标抠除白底 |
| 2.3 | 原生重写首版（引擎、棋盘、数字盘、提示 / 自动解、明暗主题） |

## 分支与许可

| 分支 | 说明 |
| --- | --- |
| `native-wpf` | 本原生绿色版（默认分支） |
| `master` | 上游原样内容（fork 保留，未改动） |

- 许可：沿用上游 MIT（见 [LICENSE](LICENSE)）；本分支代码与文档同以 MIT 发布，版权 HY-pt
- 启动页与 README 插画：AI 生成后经抠图（透明底）处理，同一张图也用于应用图标（`native/Assets/app.ico`），仅用于本项目

## 问题反馈

欢迎在 [Issues](https://github.com/HY-pt/ice-sudoku-native/issues) 反馈问题或提出建议。

## 致谢

感谢上游作者 tropicalwzc 的 ice·sudoku 与 MIT 许可。

— HY-pt
