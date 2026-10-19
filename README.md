# SSMarkdown

**基于 Unity UGUI 的运行时 Markdown 渲染组件。** 像使用 `UnityEngine.UI.Text` 一样赋值，即可在 Canvas 中显示标题、段落、强调、链接、列表、引用和代码块。

本仓库同时是一个可通过 **Unity Package Manager（UPM）** 导入的包，以及一个完全独立的演示工程。

[![Unity 2022.3 LTS](https://img.shields.io/badge/Unity-2022.3%20LTS-000000?logo=unity&logoColor=white)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-com.ssmarkdown-2b6cb0)](https://github.com/SlipperSoar/SSMarkdown)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)

> 当前版本 **1.1.3**，核心渲染能力已经完成。后续计划见 [待办事项](#待办事项)。

![SSMarkdown demo](Documentation~/demo-preview.png)

## 包信息

| 项目 | 值 |
| --- | --- |
| Package name | `com.ssmarkdown` |
| 运行时命名空间 | `SS.UIComponent` |
| 组件 | `MarkdownText : MaskableGraphic` |
| 最低 Unity 版本 | 2022.3 LTS |
| UGUI | `com.unity.ugui` 1.0.0 |
| 许可证 | MIT |

## 通过 UPM 安装

### 方式一：Git URL（推荐）

在 Unity 中打开：

**Window > Package Manager > + > Add package from git URL...**

输入：

```text
https://github.com/SlipperSoar/SSMarkdown.git#v1.1.3
```

也可以直接写入项目的 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.ssmarkdown": "https://github.com/SlipperSoar/SSMarkdown.git#v1.1.3"
  }
}
```

把 `v1.1.3` 换成 `main` 可跟踪主分支最新提交。

> 仓库是公开的，Git URL 可以直接安装。如果当前网络无法访问 GitHub，可使用下面的本地安装方式，或在 Package Manager 中指定代理。

### 方式二：本地克隆后安装

```powershell
git clone https://github.com/SlipperSoar/SSMarkdown.git
```

然后在 Package Manager 中点击 **+ > Add package from disk...**，选择仓库根目录的 `package.json`。或在目标项目的 `Packages/manifest.json` 中写相对路径：

```json
{
  "dependencies": {
    "com.ssmarkdown": "file:../../SSMarkdown"
  }
}
```

`file:` 路径相对于目标工程的 `Packages` 目录（不是工程根目录），请按实际克隆位置调整；如果包直接放在工程的 `Packages/com.ssmarkdown` 下，则写 `file:com.ssmarkdown`。

### 安装后验证

包安装成功后，Package Manager 的 **In Project** 中会出现 **SSMarkdown**，并且可以这样引用：

```csharp
using SS.UIComponent;
using UnityEngine;

public sealed class ReadmePanel : MonoBehaviour
{
    [SerializeField] private MarkdownText m_View;

    private void Start()
    {
        m_View.text = "# Hello\n\nThis is **Markdown** with a [link](https://example.com).";
        m_View.onLinkClicked.AddListener(url => Debug.Log("Clicked: " + url));
    }
}
```

也可以在编辑器中点击 **GameObject > UI > Markdown Text** 创建组件。

## 独立演示工程

演示部分已从包中移出，位于：

```text
Demo~/
```

`Demo~` 是一个完整的 Unity 工程，使用 `file:../..` 引用仓库根目录的 UPM 包，不参与包导入。

打开方式：

1. Unity Hub 选择 **Add project from disk**。
2. 选择仓库下的 `Demo~` 目录（目录名末尾的 `~` 是 UPM 排除标记，在文件系统中仍是普通目录）。
3. 使用 Unity 2022.3 打开。
4. 打开场景 `Assets/SSMarkdownDemo/Scenes/MarkdownDemo.unity`。
5. 运行后可以切换 Overview / Syntax、Light / Dark、字号，并点击链接查看 `onLinkClicked`。

包本身不包含演示场景，因此导入到其他项目时不会引入 Demo 的脚本或资源。

## 待办事项

- [x] GFM 表格解析与列宽排版
- [x] 引用式链接（reference-style links）与脚注
- [x] 任务列表 `- [ ]` / `- [x]` 的复选框绘制
- [x] 图片渲染：Resources / AssetBundle 路径、HTTP(S) URL 和加载失败占位
- [x] 语法高亮接口（不含具体语言实现）
- [ ] 代码块语言标签
- [ ] 文本选择与复制
- [ ] 链接 hover 光标、hover 高亮和键盘导航
- [ ] 中日韩字体的自动回退与更精确的断行规则
- [ ] PlayMode 测试、布局性能基准和大量文档的虚拟化
- [ ] 基于 Unity License 的 CI 导入/编译/测试流水线

欢迎通过 Issue 或 PR 补充优先级。

## 特性

- **纯 UGUI 实现**：组件 `MarkdownText` 继承 `MaskableGraphic`，自带 `ILayoutElement`、点击命中和 Maskable 支持，不依赖 TextMeshPro 或第三方库。
- **运行时赋值**：`text` 属性、`SetMarkdown(string)`、`SetMarkdown(TextAsset)` 三种方式。
- **文档级排版**：按块级元素独立排版，支持标题层级、段落间距、嵌套列表、引用缩进、代码块背景、表格和分隔线。
- **内联样式**：加粗、斜体、删除线、行内代码、链接、自动链接、HTML 实体和反斜杠转义。
- **可点击链接**：`onLinkClicked` 事件返回 URL，支持 `TryGetLinkAtLocalPosition` 做自定义悬浮提示。
- **引用式链接与脚注**：支持 `[text][id]`、短引用 `[id]`、引用图片，以及文档末尾自动汇总的 `[^id]` 脚注。
- **任务列表**：`- [ ]` / `- [x]` 自动绘制复选框，支持嵌套和主题配色。
- **语法高亮接口**：通过 `IMarkdownSyntaxHighlighter` 返回颜色区间；包内不附带具体语言实现。
- **图片**：支持 HTTP(S)、Resources 相对路径和 AssetBundle 地址，异步加载并以 RawImage 叠加渲染；失败时显示错误占位。
- **主题**：内置 Light / Dark 两套配色，切换主题时正文、标题、引用与底板一起更新。
- **ScrollRect / LayoutGroup 友好**：实现 `ILayoutElement`，搭配 `ContentSizeFitter` 自动撑高；装饰层会跟随滚动内容。
- **纯 C# 解析器**：解析器不依赖 UnityEngine，可单独在 .NET 中测试。
- **编辑器工具**：`GameObject/UI/Markdown Text` 菜单、自定义 Inspector。

## 组件 API

| 成员 | 说明 |
| --- | --- |
| `string text` | Markdown 源文本，赋值后自动重新解析和排版。 |
| `SetMarkdown(string)` | `text` 的语义化别名。 |
| `SetMarkdown(TextAsset)` | 直接赋值 `.md` 文本资源。 |
| `Font font` | 字体；为空时回退到 `LegacyRuntime.ttf` / `Arial.ttf`。 |
| `int fontSize` | 正文字号，标题按主题缩放。 |
| `TextAnchor alignment` | 水平对齐 + 整篇文档的垂直对齐。 |
| `Vector4 padding` | 左、上、右、下内边距。 |
| `MarkdownTheme theme` | 颜色、背景、间距、标题比例等。 |
| `bool enableLinks` | 是否启用链接命中与事件。 |
| `MarkdownLinkEvent onLinkClicked` | 链接点击事件，参数为 URL。 |
| `MarkdownLinkEvent onFootnoteClicked` | 脚注引用点击事件，参数为脚注 id。 |
| `IMarkdownSyntaxHighlighter codeHighlighter` | 运行时赋值；返回代码块的颜色区间。 |
| `float maxImageWidth` | 图片最大宽度；0 表示使用可用内容宽度。 |
| `float maxImageHeight` | 图片最大高度；0 表示不限制。 |
| `MarkdownImageLoader imageLoader` | 自定义图片加载器；为 null 时使用内置 HTTP / Resources / AssetBundle 加载。 |
| `ClearImageCache()` | 清空已加载图片并触发重新布局。 |
| `MarkdownDocument document` | 当前解析结果，可读取 blocks。 |
| `float contentHeight` | 当前宽度下的文档高度。 |
| `void ApplyTheme(MarkdownTheme)` | 应用主题，并同步 `theme.bodyColor` 到 `color`。 |
| `UseLightTheme()` / `UseDarkTheme()` | 应用内置主题。 |
| `void Refresh()` | 强制重新解析和排版。 |
| `bool TryGetLinkAtLocalPosition(...)` | 本地坐标链接命中，便于实现悬浮效果。 |

`MarkdownText.color` 仍然是正文和列表颜色的最终来源。`ApplyTheme()` / `UseLightTheme()` / `UseDarkTheme()` 会应用 `MarkdownTheme.bodyColor`；如果只手动修改主题里的单个字段，需要自行同步 `color` 并调用 `Refresh()`。

## 主题与对比度

`MarkdownTheme` 提供 `CreateLight()`、`CreateDark()`、`CopyFrom()`。

Dark 主题内置不透明深色 `backgroundColor` 和浅色 `bodyColor`，因此即使 `MarkdownText` 的父级背景仍是浅色，切换后标题、正文、列表和引用也都有足够对比度。将 `theme.backgroundColor` 的 alpha 调为 `0` 可以恢复透明底板；Light 主题默认透明。

演示工程会额外切换整页背景；包组件只负责自己的文档矩形。

## 图片

图片使用 `![alt](url)` 语法。为了不影响文字网格的字体图集，已加载的图片由 MarkdownText 自动创建的隐藏子层中的 `RawImage` 叠加显示。

支持的地址：

| 写法 | 说明 |
| --- | --- |
| `https://example.com/logo.png` | UnityWebRequestTexture 异步下载。 |
| `resources://Images/Logo` | 从 `Resources` 加载，可省略扩展名。 |
| `Images/Logo` | 没有协议前缀时默认按 Resources 相对路径处理。 |
| `ab://path/to/bundle#AssetName` | 从 AssetBundle 加载纹理；相对路径基于 `Application.streamingAssetsPath`。`assetbundle://` 同义。 |

示例：

```markdown
![Logo](SSMarkdownLogo)
![Remote](https://example.com/logo.png)
![Bundle](ab://ui#Logo)
```

内置加载器在第一帧先显示占位框，加载完成后自动重新布局。下载或资源加载失败时，会在原位置显示 “Image failed to load” 错误框和 alt 文本。

`maxImageWidth`（0 = 可用宽度）和 `maxImageHeight`（0 = 不限制）可以限制显示尺寸。图片不会被放大，只会按比例缩小。

如果项目使用自己的 AssetBundle 管理器，可以覆盖加载器：

```csharp
markdown.imageLoader = (url, onLoaded, onError) =>
{
    // 在自定义管理器中按 url 取 Texture2D
    Texture2D texture = MyBundleManager.LoadTexture(url);
    if (texture != null) onLoaded(texture);
    else onError("not found");
};
```

自定义加载器必须在主线程调用回调。`ClearImageCache()` 会清空缓存并重新加载。

`ab://` 的相对路径基于 `Application.streamingAssetsPath`；Android 的 StreamingAssets 不是普通文件路径，请改用自定义加载器或先复制到可读目录。

图片作为独立块参与排版：包含文字和图片的段落会被拆成“段落 + 图片 + 段落”。表格单元格和列表项内的图片语法仍显示为 alt 链接。

## 布局建议

### 自适应高度

给 `MarkdownText` 所在 GameObject 添加 `ContentSizeFitter`：

- Horizontal Fit: `Unconstrained`
- Vertical Fit: `PreferredSize`

宽度应由父级布局或 RectTransform 锚点决定。`MarkdownText` 的 `preferredWidth` 为 `0`、`flexibleWidth` 为 `1`，这是为了让文档优先占满可用宽度，而不是按最长一行无限撑宽。

### 放在 ScrollRect 中

```text
Scroll View (ScrollRect)
└─ Viewport (RectMask2D)
   └─ Markdown Content (MarkdownText + ContentSizeFitter)
```

组件会在同一个父节点下自动创建一个隐藏的装饰层，用于绘制底板、代码块背景、引用条、链接下划线和分隔线。该层会同步 RectTransform 并跟随滚动。

### 中文字体

Unity 内置的 `LegacyRuntime` 字体不包含中日韩字形。显示中文时，请导入一个覆盖所需字符的 TTF/OTF 并赋给 `font`。

## 引用式链接与脚注

引用式链接将地址集中定义，正文只保留引用 label：

```markdown
Read [the docs][docs] or the shortcut [docs].

[docs]: https://example.com/docs "Documentation"
```

支持完整引用 `[text][id]`、折叠引用 `[text][]`、短引用 `[text]`，以及引用图片 `![alt][id]`。label 不区分大小写，重复定义以第一条为准。

脚注使用 `[^id]` 引用，定义会在文档末尾统一渲染：

```markdown
A note[^1].

[^1]: Footnotes support **inline formatting**.
```

脚注引用是可点击的，点击后触发 `onFootnoteClicked`，参数为脚注 id。

## 任务列表

```markdown
- [x] Completed
- [ ] Pending
```

复选框由装饰层绘制，颜色可通过 `MarkdownTheme.taskCheckboxColor`、`taskCheckboxCheckedColor` 和 `taskCheckboxFillColor` 调整，尺寸由 `taskCheckboxSize` 控制。

## 语法高亮接口

包内只定义接口，不包含任何语言实现：

```csharp
public interface IMarkdownSyntaxHighlighter
{
    IReadOnlyList<MarkdownHighlightSpan> Highlight(string language, string code);
}
```

`MarkdownHighlightSpan` 使用 UTF-16 索引和 `Color` 前景色。重叠区间按返回顺序覆盖；返回 null 或空列表时使用主题默认代码颜色。

运行时接入：

```csharp
public sealed class MyHighlighter : IMarkdownSyntaxHighlighter
{
    public IReadOnlyList<MarkdownHighlightSpan> Highlight(string language, string code)
    {
        // 这里接入你自己的词法分析器，返回若干 MarkdownHighlightSpan 即可。
        return System.Array.Empty<MarkdownHighlightSpan>();
    }
}

markdown.codeHighlighter = new MyHighlighter();
```

语言标签目前仅作为 `language` 参数传入，不会自动显示在代码块标题中。

## Markdown 支持范围

**块级**

- `#` 到 `######` ATX 标题，以及 `===` / `---` Setext 标题
- 段落、硬换行（行尾两个空格或 `\`）
- 围栏代码块（``` 或 ~~~，保留原始文本）
- 四空格缩进代码块
- 引用，可嵌套
- 有序 / 无序列表，可嵌套
- 任务列表 `- [ ]` / `- [x]`，带复选框绘制
- GFM 表格，支持左 / 中 / 右列对齐、单元格内联样式和链接
- 分隔线

**行内**

- `**bold**` / `__bold__`
- `*italic*` / `_italic_`
- `~~strikethrough~~`
- `` `inline code` ``
- `[text](url "title")`
- `[text][id]`、`[id]` 引用式链接和引用图片
- `[^id]` 脚注引用，定义会在文档末尾汇总
- `<https://...>` 自动链接
- 裸 URL 自动链接
- `&amp;`、`&lt;`、`&#x...;` 等常见实体
- 反斜杠转义

**有意不支持（见待办）**

- 原始 HTML 样式（标签会被忽略，不参与渲染）
- 内置语言高亮实现、代码块标题栏、表格内嵌图片

解析器对未知结构采取“退化为普通文本”的策略，不会在运行时抛异常。

## 仓库结构

```text
.
├─ package.json                 # UPM package manifest
├─ Runtime/                     # 运行时程序集 SSMarkdown.Runtime
│  └─ Markdown/
├─ Editor/                      # 编辑器程序集 SSMarkdown.Editor
├─ Tests/                       # Unity EditMode 测试（testables）
├─ Demo~/                       # 独立 Unity 演示工程，不参与包导入
│  ├─ Assets/SSMarkdownDemo/
│  ├─ Packages/manifest.json    # com.ssmarkdown: file:../..
│  └─ ProjectSettings/
├─ Tools~/ParserTests/          # 无 Unity 依赖的解析器测试
├─ Documentation~/              # 截图
├─ README.md
├─ CHANGELOG.md
└─ LICENSE.md
```

渲染分为两层：

1. **文本层**：`MarkdownText` 使用 `TextGenerator` 生成字形网格，设置 `pivot = (0, 1)` 后逐块堆叠。
2. **装饰层**：独立 `MaskableGraphic` 使用白色纹理绘制实心矩形；通过字体图集之外的第二张纹理解决 UGUI Graphic 单纹理限制。

`TextGenerator` 的 `characters` 数组按源字符串索引（包含富文本标签）返回每个字符，因此字符到 `MarkdownInline` 的映射可以准确跨越颜色、粗体、斜体标签。

## 测试

纯 C# 解析器测试：

```powershell
dotnet run --project Tools~/ParserTests/ParserTests.csproj -c Release
```

Unity EditMode 测试通过 `Demo~` 工程运行（manifest 中已配置 `"testables": ["com.ssmarkdown"]`）：

1. 用 Unity 打开 `Demo~`。
2. **Window > General > Test Runner > EditMode > Run All**。

命令行方式：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath ".\Demo~" `
  -runTests -testPlatform EditMode `
  -testResults TestResults.xml
```

当前测试覆盖解析、样式、列表、引用、代码块尖括号、表格、图片加载/失败占位、链接命中、主题切换和空文档高度。

## 截图

| Light | Dark |
| --- | --- |
| ![Light](Documentation~/preview-light.png) | ![Dark](Documentation~/preview-dark.png) |

## 版本

见 [CHANGELOG.md](CHANGELOG.md)。

## License

[MIT](LICENSE.md)
