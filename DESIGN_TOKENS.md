# 设计 Token 规范（Portable）

> 本文件自包含：不引用任何工程路径、类名或外部文件。可直接作为设计/实现契约喂给其他 agent。
> 单位：尺寸为 96 DPI 下的逻辑像素（px），字体为 pt。主题：浅色单主题。

## 0. 使用规则

1. 组件只允许使用本表中的 token，禁止在组件内写死颜色、圆角、间距、字号。
2. 一个界面只使用一个主色 `Accent`，仅用于主操作、选中态、焦点态、当前进度。
3. 语义色（Success / Danger / Pending）只用于状态表达，不用于装饰。
4. 间距优先复用基础序列 `4 / 8 / 12 / 16 / 24`；出现新数值时先判断能否用序列组合。
5. 圆角只有三档：控件 8、卡片 12、胶囊 16；胶囊圆角始终等于高度的一半。所有圆角统一由共享圆角路径函数生成，禁止各控件手写圆弧。
6. 动效只用于解释状态变化，不做装饰；所有动画均为 16ms 或 40ms 定时器驱动的插值。
7. 新增 token 时，必须同步更新本文档与机器可读 JSON，并写明语义而非具体位置。
8. 焦点始终可见：键盘焦点用 1px 描边，主按钮上的焦点环使用白色 59% 透明度内嵌 3px。
9. 弹层（下拉弹层、上下文菜单）必须关闭系统阴影与原生边框，用圆角 Region 裁切 HWND，并将 1px 边框内缩绘制；边框不得贴边，确保四个外圆角完整可见。

## 1. 颜色

### 1.1 核心色

| Token | 值 | 语义 / 使用位置 |
|---|---|---|
| `FormBack` | `#F6F8FB` | 窗口背景、标签栏背景、底部操作区背景 |
| `CardBack` | `#FFFFFF` | 卡片、列表、输入框、弹出菜单、空态背景 |
| `Border` | `#DFE4EB` | 卡片 1px 边框、输入框默认边框、表头底部分隔线、菜单外框 |
| `BorderHover` | `#C1CAD6` | 输入框 / 次级按钮 hover 边框 |
| `Text` | `#1F242C` | 主文本：文件名、按钮文字、菜单项、选中行文字 |
| `Muted` | `#6E7784` | 次要文本：标签、表头、状态栏、副标题、后缀、提示 |
| `Accent` | `#2F6BFF` | 主按钮填充、选中标签文字、输入焦点边框、进度条填充 |
| `AccentHover` | `#1F5BEB` | 主按钮 hover |
| `AccentPressed` | `#174CCD` | 主按钮按下 |
| `AccentSoft` | `#EAF1FF` | 标签选中背景、「处理中」pill 背景、菜单 hover 背景 |
| `SelectedBack` | `#EAF1FF` | 列表选中行背景、下拉选中项背景 |
| `HoverBack` | `#F4F7FB` | 列表 hover 行、标签 hover、次级按钮 hover 终值 |
| `DisabledBack` | `#F1F3F6` | 禁用按钮 / 禁用控件填充 |
| `DisabledText` | `#A6ADB8` | 禁用按钮文字、禁用菜单项文字 |
| `Success` | `#1F9D55` | 完成状态文字 / 图标 |
| `SuccessSoft` | `#E8F7EE` | 「完成」pill 背景 |
| `Danger` | `#D63B3B` | 失败状态文字 |
| `DangerSoft` | `#FDECEC` | 「失败」pill 背景 |
| `PendingBack` | `#EEF1F5` | 「等待」pill 背景、滚动条轨道 |
| `PendingFore` | `#5B6472` | 「等待」pill 文字 |
| `ScrollTrack` | `#EEF1F5` | 自定义滚动条轨道 |
| `ScrollThumb` | `#CBD3DE` | 滚动条滑块 |
| `ScrollThumbHover` | `#A6B1C0` | 滚动条滑块 hover / 拖拽中 |

### 1.2 实现补充色（必须 token 化，不得散落在组件里）

| Token | 值 | 语义 / 使用位置 |
|---|---|---|
| `ControlPressedBack` | `#E8EDF5` | 次级按钮按下填充 |
| `ProgressTrack` | `#E2E7EF` | 进度条轨道 |
| `EmptyIconIdle` | `#96A0AF` | 空态图标默认颜色 |
| `EmptyIconBackIdle` | `#F1F5FA` | 空态图标默认圆形底 |
| `FocusRingOnAccent` | `#FFFFFF` @ 59% | 主按钮键盘焦点内环 |
| `DragHighlightBorder` | `Accent` @ 1.6px | 拖拽悬停时卡片边框 |

### 1.3 状态色映射

| 状态 | 背景 | 前景 | 附加元素 |
|---|---|---|---|
| 等待 | `PendingBack` | `PendingFore` | 无 |
| 处理中 | `AccentSoft` | `Accent` | 11px 旋转圆弧 |
| 完成 | `SuccessSoft` | `Success` | 无 |
| 失败 | `DangerSoft` | `Danger` | 无 |

## 2. 圆角

| Token | 值 | 使用位置 |
|---|---|---|
| `RadiusControl` | `8` | 按钮、输入框、下拉框、数字框、菜单外框、菜单项 hover |
| `RadiusCard` | `12` | 工作区卡片 |
| `RadiusPill` | `16` | 标签胶囊（高度 32，等于半高） |
| `RadiusAppIcon` | `14` | 64×64 应用图标本体（58×58，内缩 3） |

> 规则：状态 pill 的圆角 = 高度 / 2（22px 高 → 11）。滚动条滑块圆角 = 宽度 / 2。

## 3. 间距与尺寸

### 3.1 基础间距

`xs=4` · `sm=8` · `md=12` · `lg=16` · `xl=24`

### 3.2 页面骨架

```
┌──────────────────────────────────────────────────────┐
│ TabStrip  h=52, 左起 16, 胶囊 h=32, 间距 4            │
├──────────────────────────────────────────────────────┤
│ 外距 16/12/16/8  ┌─────────────────────────────────┐  │
│                  │ 工具栏 h=52（选项 … | +添加文件）│  │
│                  │ 内边距 16                        │  │
│                  │─────────────────────────────────│  │
│                  │ 表头 h=36                        │  │
│                  │ 行    h=36                       │  │
│                  │ …                                │  │
│                  └─────────────────────────────────┘  │
├──────────────────────────────────────────────────────┤
│ 进度条 h=3（底部区顶部，左右 16）                     │
│ 状态文字 …        计数 …   [清空]  [主操作]    h=56   │
└──────────────────────────────────────────────────────┘
```

### 3.3 尺寸

| Token / 项 | 值 |
|---|---|
| 标签栏高度 | `52` |
| 标签胶囊高度 | `32` |
| 标签胶囊水平内边距合计 | `32`（文本宽 + 32） |
| 标签左起 / 胶囊间距 | `16` / `4` |
| 工具栏高度 | `52` |
| 工具栏选项上下留白 | `10` |
| 选项组：标签与控件间距 | `8` |
| 选项组之间间距 | `24` |
| 卡片宿主外距 | `16, 12, 16, 8` |
| 卡片内边距 | `16` |
| 卡片边框 | `1px` |
| 列表表头高度 | `36` |
| 列表行高 | `36` |
| 列表单元格左右内边距 | `10` / `8` |
| 列表选中 / 悬停背景内缩 | 水平 `4` / 垂直 `2` |
| 列宽规则 | 文件列 `Fill`（占满剩余宽度），状态列固定 `140` |
| 文件列最小宽度 | `140` |
| 状态表头 / 状态 pill 对齐 | 水平居中 |
| 输入控件高度 | `32` |
| 下拉框宽度 / 窄下拉宽度 | `132` / `120` |
| 数字框宽度 | `132` |
| 数字框内边距 | `10` |
| 添加按钮 | `104 × 32` |
| 清空按钮 | `96 × 34` |
| 主操作按钮 | `116 × 34` |
| 底部操作区高度 | `56` |
| 底部按钮顶部位置 | `11` |
| 底部右边距 / 按钮间距 / 计数与按钮间距 | `16` / `8` / `12` |
| 状态文字位置 | `x=16, y=18` |
| 计数文字宽 / 对齐 | `160` / 右对齐 |
| 进度条高度 | `3` |
| 状态 pill 高度 | `22` |
| 状态 pill 宽度 | 文本宽 + `22`；“处理中”再加 `15` 给旋转指示 |
| 旋转指示直径 / 线宽 / 圆弧 | `11` / `2` / `250°` |
| 滚动条列宽 | `12` |
| 滚动条可视轨道宽 | `4`（右边距 8，上下内缩 2） |
| 滚动条滑块最小高度 | `24` |
| 空态图标直径 | `44` |
| 空态图标与标题间距 | `14` |
| 空态标题行高 / 副标题行高 | `26` / `20` |
| 菜单内边距 | `1, 4, 1, 4` |
| 菜单项内边距 | `12, 6, 12, 6` |
| 菜单项 hover 内缩 | 左右 `4`，上下 `1` |
| 菜单分隔线左右内缩 | `12` |
| 弹层边框内缩 / 内容内边距 | `1` / `2` |
| 下拉列表行高 / 最大可见行数 | `30` / `8` |

## 4. 字体

| 用途 | 字族 | 字号 | 字重 |
|---|---|---|---|
| 全局 / 正文 / 按钮 / 列表 | `Microsoft YaHei UI` | `9` | Regular |
| 标签胶囊 | `Microsoft YaHei UI` | `9.5` | Regular |
| 下拉列表项 | `Microsoft YaHei UI` | `9` | Regular |
| 表头 | 继承全局 | `9` | Bold |
| 空态标题 | `Microsoft YaHei UI` | `12` | Bold |
| 空态副标题 | `Microsoft YaHei UI` | `9.5` | Regular |

> 文本测量与绘制统一使用 `TextRenderer`（ClearType），不要与 `Graphics.DrawString` 混用；后者字宽不同会导致对齐漂移。

## 5. 组件状态矩阵

### 5.1 主按钮（Accent）

| 状态 | 填充 | 边框 | 文字 |
|---|---|---|---|
| Default | `Accent` | `Accent` | `#FFFFFF` |
| Hover | `AccentHover`（16ms 插值） | 同填充 | `#FFFFFF` |
| Pressed | `AccentPressed` | 同填充 | `#FFFFFF` |
| Disabled | `DisabledBack` | `Border` | `DisabledText` |
| Focus | — | 内缩 3px 的 `#FFFFFF` 59% 环，圆角 5 | — |

### 5.2 次级按钮

| 状态 | 填充 | 边框 | 文字 |
|---|---|---|---|
| Default | `CardBack` | `Border` | `Text` |
| Hover | 由白向 `HoverBack` 插值 | `Border` → `BorderHover` | `Text` |
| Pressed | `ControlPressedBack` | `BorderHover` | `Text` |
| Disabled | `DisabledBack` | `Border` | `DisabledText` |
| Focus | — | 内缩 3px 的 `Accent` 环，圆角 5 | — |

### 5.3 输入框 / 下拉框 / 数字框

| 状态 | 填充 | 边框 |
|---|---|---|
| Default | `CardBack` | `Border` |
| Hover | `CardBack` | `BorderHover` |
| Focus / 展开 | `CardBack` | `Accent` |
| Disabled（扩展时） | `DisabledBack` | `Border`，文字 `DisabledText` |

- 文本左内边距：`10`。
- 下拉箭头：右距 `17`，`Muted`，1.6px，圆头。
- 数字框后缀：`Muted`，右内边距 `10`。

### 5.4 标签胶囊

| 状态 | 背景 | 文字 |
|---|---|---|
| Default | 透明 | `Muted` |
| Hover | `HoverBack` | `Text` |
| Selected | `AccentSoft` | `Accent` |

### 5.5 列表

| 区域 / 状态 | 背景 | 文字 |
|---|---|---|
| 表头 | `CardBack` | `Muted`，Bold 9 |
| 普通行 | `CardBack` | `Text` |
| Hover 行 | `HoverBack` | `Text` |
| 选中行 | `SelectedBack` | `Text` |
| 状态列 | 随行背景 | 由状态 pill 决定 |

- 表头底部 1px 分隔线：`Border`，左右内缩 `12`。
- 列表无网格线、无行头、无原生滚动条。

### 5.6 上下文菜单

| 状态 | 表现 |
|---|---|
| 背景 | `CardBack` |
| 外框 | `Border` 1px，外圆角 `8`；边框内缩 `1px` 绘制 |
| HWND | 圆角 `8` 的 Region 裁切，四角完整可见 |
| 文字 | `Text`；禁用 `DisabledText` |
| Hover | `AccentSoft` 圆角 `8`，内缩 4 / 1 |
| 分隔线 | `Border`，左右内缩 `12` |
| 阴影 / 原生边框 | 全部关闭 |

### 5.7 滚动条

| 状态 | 颜色 |
|---|---|
| 轨道 | `ScrollTrack`，宽 4，圆角 2 |
| 滑块 | `ScrollThumb` |
| Hover / 拖拽 | `ScrollThumbHover` |

### 5.8 卡片与空态

| 元素 | 状态 | 表现 |
|---|---|---|
| 卡片 | Default | `CardBack` 填充，`Border` 1px，圆角 12 |
| 卡片 | 拖拽悬停 | `Accent` 1.6px 边框 |
| 空态图标底 | Default / Hover | `EmptyIconBackIdle` / `AccentSoft` |
| 空态图标 | Default / Hover | `EmptyIconIdle` / `Accent` |
| 空态标题 | — | `Text`，12 Bold |
| 空态副标题 | — | `Muted`，9.5 |

### 5.9 进度条

| 部分 | 颜色 | 几何 |
|---|---|---|
| 轨道 | `ProgressTrack` | 高 3，圆角 1.5 |
| 填充 | `Accent` | 高 3，圆角 1.5，最小宽度 = 高度 |

## 6. 组件配方

### 6.1 按钮
- 圆角 `8`；高度：工具栏按钮 `32`，底部按钮 `34`。
- 文字水平垂直居中，无内边距偏移。
- Hover 通过 16ms 定时器向目标色插值（系数 0.35），差值 < 0.01 时停止。
- 焦点环：矩形内缩 3px，圆角 `max(4, 8-3)=5`。

### 6.2 卡片
- 圆角 `12`，1px 边框，背景 `CardBack`。
- 绘制矩形内缩 1px，避免边缘抗锯齿被裁切。
- 卡片内容内边距 `16`；页面在卡片外再留 `16, 12, 16, 8`。

### 6.3 标签栏
- 高 `52`，胶囊垂直居中（`(52-32)/2=10`）。
- 每个胶囊宽度 = 文本测量宽 + `32`，间距 `4`，从 `x=16` 开始。
- 选中/悬停背景为整颗胶囊，不是下划线。

### 6.4 列表
- 表头与行高均为 `36`；文件列 `Fill`（最小 `140`），状态列固定 `140`。
- 状态表头与状态 pill 水平居中；文件名超长用省略号，tooltip 提供完整路径。
- 行背景在行预处理阶段横跨整行绘制（选中 `SelectedBack` / hover `HoverBack` / 默认 `CardBack`），不得按单元格分裂或在状态列右侧留白。
- 状态 pill：高 `22`，圆角 = 高 / 2 = `11`，在状态列内水平居中；文字左侧留 `11`。
- 处理中 pill 在文字前绘制 11px 圆弧（2px 线宽、250°、圆头），每 40ms 旋转 30°，只重绘处理中的行。
- Hover 通过命中测试记录行号，只重绘旧行与新行。

### 6.5 空态
- 图标圆直径 `44`，与标题间距 `14`。
- 标题高 `26`，副标题高 `20`，整体在容器内垂直居中。
- 整块可点击，等同于“添加文件”；支持 Enter / Space。

### 6.6 下拉框
- 控件本体 `132×32`（窄版 `120×32`），圆角 `8`。
- 弹出层：列表行高 `30`，最多显示 `8` 行，宽度与控件一致；内容内边距 `2`，外框在内容外再扩 `2` 用于内缩边框。
- 弹层背景 `CardBack`、边框 `Border`、圆角 `8`，无阴影、无原生边框，HWND 使用圆角 Region。
- 选中项：`SelectedBack` + `Accent` 文字；hover 项：`HoverBack` + `Text`。

### 6.7 数字框
- 外观与输入框一致；只接受数字，失焦时空值归零、超上限钳制。
- 后缀以 `Muted` 绘制在右侧，编辑区宽度 = 总宽 - 左右 10 - 后缀宽。

### 6.8 滚动条
- 独立 12px 宽控件，轨道宽 4、右距 8、上下内缩 2、圆角 2。
- 滑块高度 = 轨道高 × (可见行 / 总行)，最小 `24`。
- 支持拖动（带鼠标捕获）与点击轨道翻页；滚轮映射为 3 行。

### 6.9 弹层容器（RoundedPopup）
- 下拉弹层与上下文菜单共用同一套弹层容器。
- 关闭 `DropShadowEnabled`，不显示系统阴影；不使用原生边框，由共享渲染器绘制背景与边框。
- 在打开与尺寸变化时，把 HWND 设为圆角 Region（圆角 = 控件档 `8`）。
- 边框以 `1px` 内缩绘制，圆角取 `8 - 1 = 7`，避免右侧、底部与四角被 Region 裁切。
- 背景 `CardBack`、边框 `Border`；内容与边框之间保留 `2px` 内边距。

### 6.10 应用图标
- 64×64 画布，透明背景；本体 58×58、内缩 3、圆角 `14`、填充 `Accent`。
- 白色向上箭头：线宽 `5`，圆头；竖线 `(32,45)→(32,23)`，箭头两翼 `(21,33)→(32,23)`、`(43,33)→(32,23)`。

## 7. 动效

| 动效 | 参数 |
|---|---|
| 按钮 hover 渐入 | 每 `16ms` 向目标插值 `35%`：`t += (target - t) * 0.35`；差值 `< 0.01` 时归位并停止 |
| 按钮按压 | 每 `16ms` 向目标插值 `50%`；按下时圆角矩形内缩 `1px`、文字下移 `1px`；释放后回弹 |
| Tab hover 渐入 | 每 `16ms` 向目标插值 `35%`，背景在 `FormBack` 与 `HoverBack` 间插值 |
| Tab 选中指示滑动 | 每 `16ms` 向目标位置/宽度插值 `25%`；位置与宽度差值均 `< 0.5px` 时归位并停止 |
| 列表行 hover 渐入 | 每 `16ms` 向目标插值 `35%`，背景在 `CardBack` 与 `HoverBack` 间插值；离开时反向淡出 |
| 状态 pill 切换 | 每 `16ms` 向 `1` 插值 `25%`，背景/前景色在前一状态与当前状态之间插值 |
| 空态 / 下拉框 hover | 与按钮 hover 相同：`16ms`、`35%`；空态图标色与底色同步插值 |
| 状态文字渐隐 | 每 `16ms` 前进 `0.035`，约 `457ms` 从语义色回到 `Muted` |
| 「处理中」旋转 | 每 `40ms` 旋转 `30°`，圆弧 `250°`，线宽 `2`，圆头 |
| 进度条追赶 | 每 `16ms` 向目标插值 `22%`；差值 `< 0.2` 时归位并停止；插值后差值 `< 0.6` 也立即归位；`Snap()` 立即归位 |
| 输入框焦点 | 无过渡，立即切换边框色 |
| 拖拽高亮 | 无过渡，立即切换边框色与线宽 |

## 8. 机器可读版本

> 与 `design-tokens.json` 内容一致；本块可直接复制使用，不依赖任何外部文件。

```json
{
  "meta": {
    "name": "Toolbox Light",
    "version": "1.0.0",
    "theme": "light",
    "selfContained": true,
    "units": { "size": "px@96dpi", "font": "pt" }
  },
  "color": {
    "formBack": "#F6F8FB",
    "cardBack": "#FFFFFF",
    "border": "#DFE4EB",
    "borderHover": "#C1CAD6",
    "text": "#1F242C",
    "muted": "#6E7784",
    "accent": "#2F6BFF",
    "accentHover": "#1F5BEB",
    "accentPressed": "#174CCD",
    "accentSoft": "#EAF1FF",
    "selectedBack": "#EAF1FF",
    "hoverBack": "#F4F7FB",
    "disabledBack": "#F1F3F6",
    "disabledText": "#A6ADB8",
    "success": "#1F9D55",
    "successSoft": "#E8F7EE",
    "danger": "#D63B3B",
    "dangerSoft": "#FDECEC",
    "pendingBack": "#EEF1F5",
    "pendingFore": "#5B6472",
    "scrollTrack": "#EEF1F5",
    "scrollThumb": "#CBD3DE",
    "scrollThumbHover": "#A6B1C0"
  },
  "colorSupplemental": {
    "controlPressedBack": "#E8EDF5",
    "progressTrack": "#E2E7EF",
    "emptyIconIdle": "#96A0AF",
    "emptyIconBackIdle": "#F1F5FA",
    "focusRingOnAccent": { "hex": "#FFFFFF", "alpha": 150 },
    "dragHighlightBorder": { "ref": "color.accent", "width": 1.6 }
  },
  "semantic": {
    "status": {
      "waiting": { "background": "#EEF1F5", "foreground": "#5B6472" },
      "processing": { "background": "#EAF1FF", "foreground": "#2F6BFF", "spinner": true },
      "done": { "background": "#E8F7EE", "foreground": "#1F9D55" },
      "failed": { "background": "#FDECEC", "foreground": "#D63B3B" }
    }
  },
  "radius": {
    "control": 8,
    "card": 12,
    "pill": 16,
    "appIcon": 14
  },
  "spacing": {
    "xs": 4,
    "sm": 8,
    "md": 12,
    "lg": 16,
    "xl": 24
  },
  "size": {
    "tabStripHeight": 52,
    "tabPillHeight": 32,
    "tabPillPaddingX": 32,
    "tabInsetLeft": 16,
    "tabGap": 4,
    "toolbarHeight": 52,
    "toolbarPaddingY": 10,
    "optionGroupGap": 24,
    "optionLabelGap": 8,
    "cardHostPadding": [16, 12, 16, 8],
    "cardPadding": 16,
    "cardBorder": 1,
    "gridHeaderHeight": 36,
    "gridRowHeight": 36,
    "gridCellPaddingX": [10, 8],
    "gridRowHighlightInsetX": 4,
    "gridRowHighlightInsetY": 2,
    "columnMinWidthFile": 140,
    "stateColumnWidth": 140,
    "controlHeight": 32,
    "comboWidth": 132,
    "comboWidthNarrow": 120,
    "numberBoxWidth": 132,
    "numberBoxPadding": 10,
    "addButton": [104, 32],
    "clearButton": [96, 34],
    "startButton": [116, 34],
    "bottomBarHeight": 56,
    "bottomButtonTop": 11,
    "bottomRightInset": 16,
    "bottomButtonGap": 8,
    "bottomCountGap": 12,
    "statusTextTop": 18,
    "countLabelWidth": 160,
    "progressHeight": 3,
    "progressInsetX": 16,
    "statusPillHeight": 22,
    "statusPillWidthBase": 22,
    "statusPillSpinnerExtra": 15,
    "spinnerDiameter": 11,
    "spinnerStroke": 2,
    "spinnerArcDegrees": 250,
    "scrollColumnWidth": 12,
    "scrollTrackWidth": 4,
    "scrollTrackInsetRight": 8,
    "scrollTrackInsetY": 2,
    "scrollThumbMinHeight": 24,
    "emptyIconDiameter": 44,
    "emptyIconGap": 14,
    "emptyTitleHeight": 26,
    "emptySubtitleHeight": 20,
    "menuPadding": [1, 4, 1, 4],
    "menuItemPadding": [12, 6, 12, 6],
    "menuHoverInset": [4, 1],
    "menuSeparatorInset": 12,
    "popupBorderInset": 1,
    "popupPadding": 2,
    "dropdownRowHeight": 30,
    "dropdownMaxRows": 8
  },
  "font": {
    "family": "Microsoft YaHei UI",
    "base": 9,
    "tab": 9.5,
    "header": 9,
    "headerWeight": "bold",
    "emptyTitle": 12,
    "emptyTitleWeight": "bold",
    "emptySubtitle": 9.5,
    "dropdown": 9
  },
  "motion": {
    "hoverFrameMs": 16,
    "hoverLerp": 0.35,
    "hoverStopThreshold": 0.01,
    "pressFrameMs": 16,
    "pressLerp": 0.5,
    "pressInset": 1,
    "tabIndicatorFrameMs": 16,
    "tabIndicatorLerp": 0.25,
    "tabIndicatorStopThreshold": 0.5,
    "rowHoverFrameMs": 16,
    "rowHoverLerp": 0.35,
    "statePillFrameMs": 16,
    "statePillLerp": 0.25,
    "statusFadeFrameMs": 16,
    "statusFadeStep": 0.035,
    "spinnerFrameMs": 40,
    "spinnerDegrees": 30,
    "progressFrameMs": 16,
    "progressLerp": 0.22,
    "progressStopThreshold": 0.2,
    "progressSnapThreshold": 0.6,
    "scrollWheelRows": 3
  }
}
```
