# Native Article Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ArticleContentPage`'s WebView2 reader with a native UWP article page backed by `/x/article/view`, supporting both legacy HTML and current JSON article bodies.

**Architecture:** A small API wrapper and `ArticleVM` load article data through the existing request stack. Pure C# parameter and content parsers normalize both response formats into typed content blocks, while the page and a focused text control render those blocks with native XAML and route links through `MessageCenter`.

**Tech Stack:** C# 7-compatible code, UWP XAML, `ApiModel.Request()`, Newtonsoft.Json 13.0.4, HtmlAgilityPack 1.11.18, MSTest in an isolated .NET 8 parser-test project, Visual Studio MSBuild for the UWP application.

---

## Repository Gates

Before each task that changes repository files, explain the exact paths and obtain explicit user confirmation. Before every commit, read the current `AGENTS.md` and `git status --short`, stage only the listed paths, and obtain confirmation for the Git-history change. Use `git commit -S`, include `Co-Authored-By: Codex <noreply@openai.com>`, then verify with `git log -1 --show-signature`.

Do not run `dotnet build` against `BiliBili.sln` or `BiliBili.UWP.csproj`. The standalone parser test project may use `dotnet test`; the UWP application must use Visual Studio MSBuild.

## File Map

**Create:**

- `BiliBili.UWP/Api/ArticleAPI.cs`: creates the article-view `ApiModel`.
- `BiliBili.UWP/Models/ArticleModels.cs`: API DTOs plus renderer-neutral content-block models.
- `BiliBili.UWP/Modules/ArticleParameterParser.cs`: accepts numeric IDs, `cv` identifiers, protocol URLs, and web URLs.
- `BiliBili.UWP/Modules/ArticleContentParser.cs`: normalizes HTML, JSON Delta, and limited Opus fallback content.
- `BiliBili.UWP/Modules/ArticleVM.cs`: owns loading, errors, article data, and stale-request protection.
- `BiliBili.UWP/Controls/ArticleTextBlockControl.xaml`: hosts a native `RichTextBlock`.
- `BiliBili.UWP/Controls/ArticleTextBlockControl.xaml.cs`: maps inline models to UWP inline elements and raises link events.
- `tools/ArticleParserTests/ArticleParserTests.csproj`: isolated test runner for pure parser code.
- `tools/ArticleParserTests/ArticleParameterParserTests.cs`: navigation-parameter cases.
- `tools/ArticleParserTests/ArticleContentParserTests.cs`: HTML and JSON normalization assertions.
- `tools/ArticleParserTests/Fixtures/type0.json`: compact legacy HTML response data.
- `tools/ArticleParserTests/Fixtures/type3.json`: compact JSON Delta response data.

**Modify:**

- `BiliBili.UWP/Pages/FindMore/ArticleContentPage.xaml`: replace WebView2 with the native reading layout and block templates.
- `BiliBili.UWP/Pages/FindMore/ArticleContentPage.xaml.cs`: load the VM, render states, handle links/cards/share, and remove WebView2 behavior.
- `BiliBili.UWP/BiliBili.UWP.csproj`: explicitly include new API, model, module, and control files.

The parser test project is not added to `BiliBili.sln`; this avoids changing UWP solution platform mappings or making the app build depend on .NET 8.

### Task 1: Normalize Article Navigation Parameters

**Files:**

- Create: `tools/ArticleParserTests/ArticleParserTests.csproj`
- Create: `tools/ArticleParserTests/ArticleParameterParserTests.cs`
- Create: `BiliBili.UWP/Modules/ArticleParameterParser.cs`
- Modify: `BiliBili.UWP/BiliBili.UWP.csproj`

- [ ] **Step 1: Create the isolated test project**

Create `tools/ArticleParserTests/ArticleParserTests.csproj` with linked production sources so tests exercise the exact files compiled by UWP:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="HtmlAgilityPack" Version="1.11.18" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.4" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.4" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="..\..\BiliBili.UWP\Modules\ArticleParameterParser.cs" Link="Production\ArticleParameterParser.cs" />
  </ItemGroup>
  <ItemGroup>
    <None Update="Fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write parameter parsing tests**

Create tests covering every currently reachable argument shape:

```csharp
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleParserTests
{
    [TestClass]
    public class ArticleParameterParserTests
    {
        [DataTestMethod]
        [DataRow("123", 123L)]
        [DataRow("cv123", 123L)]
        [DataRow("https://www.bilibili.com/read/cv123", 123L)]
        [DataRow("https://www.bilibili.com/read/app/123", 123L)]
        [DataRow("https://www.bilibili.com/read/mobile/123", 123L)]
        [DataRow("bilibili://article/123", 123L)]
        public void TryParse_AcceptsReachableShapes(string input, long expected)
        {
            Assert.IsTrue(ArticleParameterParser.TryParse(input, out var actual));
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TryParse_UnwrapsNavigationArray()
        {
            Assert.IsTrue(ArticleParameterParser.TryParse(new object[] { "cv456" }, out var actual));
            Assert.AreEqual(456L, actual);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("cv0")]
        [DataRow("https://example.com/read/cv123")]
        [DataRow("not-an-article")]
        public void TryParse_RejectsInvalidValues(string input)
        {
            Assert.IsFalse(ArticleParameterParser.TryParse(input, out _));
        }

        [TestMethod]
        public void TryParse_RejectsNull()
        {
            Assert.IsFalse(ArticleParameterParser.TryParse(null, out _));
        }
    }
}
```

- [ ] **Step 3: Run the test and observe the expected failure**

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj --filter ArticleParameterParserTests
```

Expected: build fails because `ArticleParameterParser` does not exist yet. This establishes a failure caused only by the missing target implementation.

- [ ] **Step 4: Implement `ArticleParameterParser`**

Use one anchored set of accepted forms and reject unrelated hosts:

```csharp
using System;
using System.Text.RegularExpressions;

namespace BiliBili.UWP.Modules
{
    public static class ArticleParameterParser
    {
        private static readonly Regex ArticlePath = new Regex(
            @"^(?:cv|(?:https?://(?:www\.)?bilibili\.com/read/(?:cv|app/|mobile/))|bilibili://article/)(\d+)(?:[/?#].*)?$",
            RegexOptions.IgnoreCase);

        public static bool TryParse(object parameter, out long articleId)
        {
            articleId = 0;
            if (parameter is object[] values)
            {
                parameter = values.Length > 0 ? values[0] : null;
            }
            if (parameter == null)
            {
                return false;
            }
            if (parameter is long longValue)
            {
                articleId = longValue;
                return articleId > 0;
            }
            if (parameter is int intValue)
            {
                articleId = intValue;
                return articleId > 0;
            }

            var value = parameter.ToString().Trim();
            if (long.TryParse(value, out articleId))
            {
                return articleId > 0;
            }
            var match = ArticlePath.Match(value);
            return match.Success &&
                long.TryParse(match.Groups[1].Value, out articleId) &&
                articleId > 0;
        }
    }
}
```

- [ ] **Step 5: Add the UWP project include**

Add this adjacent to the other module entries in `BiliBili.UWP.csproj`:

```xml
<Compile Include="Modules\ArticleParameterParser.cs" />
```

Do not expect the test project to compile until the linked model and content-parser files are created in Task 2.

### Task 2: Define Content Models and Parse Legacy HTML

**Files:**

- Create: `BiliBili.UWP/Models/ArticleModels.cs`
- Create: `BiliBili.UWP/Modules/ArticleContentParser.cs`
- Create: `tools/ArticleParserTests/Fixtures/type0.json`
- Create: `tools/ArticleParserTests/ArticleContentParserTests.cs`
- Modify: `tools/ArticleParserTests/ArticleParserTests.csproj`
- Modify: `BiliBili.UWP/BiliBili.UWP.csproj`

- [ ] **Step 1: Add the legacy fixture**

Create a complete, synthetic API `data` object that exercises supported HTML without depending on live content:

```json
{
  "id": 1,
  "type": 0,
  "title": "旧版专栏",
  "content": "<h2>章节</h2><p>普通<strong>加粗</strong><em>斜体</em><a href=\"https://www.bilibili.com/video/av1\">链接</a></p><blockquote>引用</blockquote><ul><li>项目一</li></ul><img src=\"https://i0.hdslb.com/test.jpg\" alt=\"示例图\" width=\"640\" height=\"360\"><hr>",
  "author": { "mid": 2, "name": "作者", "face": "https://i0.hdslb.com/face.jpg" },
  "category": { "id": 3, "name": "科技" },
  "publish_time": 1744789930,
  "stats": { "view": 10, "like": 2, "favorite": 1 }
}
```

- [ ] **Step 2: Write the failing HTML parser test**

Add this method to `ArticleContentParserTests.cs`:

```csharp
using System.IO;
using System.Linq;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ArticleParserTests
{
    [TestClass]
    public class ArticleContentParserTests
    {
        [TestMethod]
        public void Parse_LegacyHtml_PreservesSupportedBlocksAndInlines()
        {
            var json = File.ReadAllText(Path.Combine("Fixtures", "type0.json"));
            var article = JsonConvert.DeserializeObject<ArticleDataModel>(json);
            var blocks = new ArticleContentParser().Parse(article).ToList();

            CollectionAssert.AreEqual(
                new[] { ArticleBlockType.Text, ArticleBlockType.Text, ArticleBlockType.Text,
                    ArticleBlockType.Text, ArticleBlockType.Image, ArticleBlockType.Separator },
                blocks.Select(item => item.Type).ToArray());
            var heading = (ArticleTextBlockModel)blocks[0];
            Assert.AreEqual(ArticleTextKind.Heading, heading.Kind);
            Assert.AreEqual(2, heading.HeadingLevel);
            var paragraph = (ArticleTextBlockModel)blocks[1];
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Bold && item.Text == "加粗"));
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Italic && item.Text == "斜体"));
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Link.EndsWith("/video/av1")));
            Assert.AreEqual(ArticleTextKind.Quote, ((ArticleTextBlockModel)blocks[2]).Kind);
            Assert.AreEqual(ArticleTextKind.Bullet, ((ArticleTextBlockModel)blocks[3]).Kind);
            var image = (ArticleImageBlockModel)blocks[4];
            Assert.AreEqual(640, image.Width);
            Assert.AreEqual(360, image.Height);
        }
    }
}
```

- [ ] **Step 3: Run the suite and verify it still fails**

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj
```

Expected: compile errors for the missing article models and content parser.

- [ ] **Step 4: Implement renderer-neutral models**

Define these exact public contracts in `ArticleModels.cs`; keep the file free of `Windows.UI.Xaml` types so the standalone tests compile it:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BiliBili.UWP.Models
{
    public class ArticleDataModel
    {
        public long id { get; set; }
        public int type { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public ArticleAuthorModel author { get; set; }
        public ArticleCategoryModel category { get; set; }
        public long publish_time { get; set; }
        public ArticleStatsModel stats { get; set; }
        public JObject opus { get; set; }
    }

    public class ArticleAuthorModel { public long mid { get; set; } public string name { get; set; } public string face { get; set; } }
    public class ArticleCategoryModel { public long id { get; set; } public string name { get; set; } }
    public class ArticleStatsModel { public long view { get; set; } public long like { get; set; } public long favorite { get; set; } }

    public enum ArticleBlockType { Text, Image, Separator, Embed, Unknown }
    public enum ArticleTextKind { Paragraph, Heading, Quote, Bullet, Ordered }
    public enum ArticleEmbedType { Video, Article, Vote, Live }

    public abstract class ArticleBlockModel
    {
        protected ArticleBlockModel(ArticleBlockType type) { Type = type; }
        public ArticleBlockType Type { get; }
    }

    public class ArticleInlineModel
    {
        public string Text { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Strike { get; set; }
        public string Color { get; set; }
        public string Link { get; set; }
    }

    public class ArticleTextBlockModel : ArticleBlockModel
    {
        public ArticleTextBlockModel() : base(ArticleBlockType.Text) { Inlines = new List<ArticleInlineModel>(); }
        public ArticleTextKind Kind { get; set; }
        public int HeadingLevel { get; set; }
        public int ListLevel { get; set; }
        public int ListOrder { get; set; }
        public string Alignment { get; set; }
        public List<ArticleInlineModel> Inlines { get; }
    }

    public class ArticleImageBlockModel : ArticleBlockModel
    {
        public ArticleImageBlockModel() : base(ArticleBlockType.Image) { }
        public string Url { get; set; }
        public string Alt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class ArticleSeparatorBlockModel : ArticleBlockModel
    {
        public ArticleSeparatorBlockModel() : base(ArticleBlockType.Separator) { }
    }

    public class ArticleEmbedBlockModel : ArticleBlockModel
    {
        public ArticleEmbedBlockModel() : base(ArticleBlockType.Embed) { }
        public ArticleEmbedType EmbedType { get; set; }
        public string Id { get; set; }
        public string CoverUrl { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
        public string TypeText
        {
            get
            {
                switch (EmbedType)
                {
                    case ArticleEmbedType.Video: return "视频";
                    case ArticleEmbedType.Article: return "专栏";
                    case ArticleEmbedType.Vote: return "投票";
                    case ArticleEmbedType.Live: return "直播";
                    default: return "内容";
                }
            }
        }
        public string DisplayTitle { get { return string.IsNullOrWhiteSpace(Title) ? TypeText + "卡片" : Title; } }
    }

    public class ArticleUnknownBlockModel : ArticleBlockModel
    {
        public ArticleUnknownBlockModel() : base(ArticleBlockType.Unknown) { }
        public string Description { get; set; }
    }
}
```

- [ ] **Step 5: Implement the HTML parser path**

Implement `ArticleContentParser.Parse()` as a format dispatcher and use HtmlAgilityPack DOM traversal for `type=0`:

```csharp
public IReadOnlyList<ArticleBlockModel> Parse(ArticleDataModel article)
{
    if (article == null) throw new ArgumentNullException(nameof(article));
    if (article.type == 0) return ParseHtml(article.content);
    if (article.type == 3) return ParseDeltaOrOpus(article.content, article.opus);
    throw new FormatException("不支持的专栏正文类型：" + article.type);
}
```

The HTML traversal must implement these deterministic rules:

```text
h1-h6       -> Text, Kind=Heading, HeadingLevel parsed from tag
p            -> Text, Kind=Paragraph
blockquote   -> Text, Kind=Quote
li under ol  -> Text, Kind=Ordered, order counted from 1
li under ul  -> Text, Kind=Bullet
img          -> Image using data-src first, then src; parse width/height as positive integers
hr           -> Separator
script/style -> ignored with descendants
other nodes  -> recurse in document order
```

When constructing each text block, recursively collect text nodes and `<br>` boundaries. Carry inherited `<strong>/<b>`, `<em>/<i>`, `<s>/<del>`, `<font color>`, inline CSS `color` declarations, and `<a href>` state into `ArticleInlineModel`. Decode HTML entities with `HtmlEntity.DeEntitize`, merge adjacent inlines only when all style fields match, and discard whitespace-only blocks.

- [ ] **Step 6: Link production parser files, add UWP includes, and run tests**

Add these links to `ArticleParserTests.csproj` beside the parameter-parser link:

```xml
<Compile Include="..\..\BiliBili.UWP\Models\ArticleModels.cs" Link="Production\ArticleModels.cs" />
<Compile Include="..\..\BiliBili.UWP\Modules\ArticleContentParser.cs" Link="Production\ArticleContentParser.cs" />
```

Add these adjacent to the existing model/module entries:

```xml
<Compile Include="Models\ArticleModels.cs" />
<Compile Include="Modules\ArticleContentParser.cs" />
```

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj
```

Expected: all parameter and legacy HTML tests pass.

- [ ] **Step 7: Commit the pure parsing foundation after explicit Git confirmation**

Stage only the Task 1 and Task 2 paths. Suggested signed commit:

```text
实现专栏参数与旧版正文解析

- 统一解析专栏导航参数并拒绝无关地址
- 增加纯内容模型、HTML 白名单解析与独立样本测试

Co-Authored-By: Codex <noreply@openai.com>
```

### Task 3: Parse Current JSON Delta and Opus Fallback Content

**Files:**

- Create: `tools/ArticleParserTests/Fixtures/type3.json`
- Modify: `tools/ArticleParserTests/ArticleContentParserTests.cs`
- Modify: `BiliBili.UWP/Modules/ArticleContentParser.cs`

- [ ] **Step 1: Add a JSON Delta fixture**

Create a compact article containing block formatting and every agreed embedded type:

```json
{
  "id": 41358718,
  "type": 3,
  "title": "新版专栏",
  "content": "{\"ops\":[{\"insert\":\"章节\"},{\"attributes\":{\"header\":2},\"insert\":\"\\n\"},{\"attributes\":{\"bold\":true,\"link\":\"https://www.bilibili.com/video/av1\"},\"insert\":\"正文\"},{\"attributes\":{\"blockquote\":true},\"insert\":\"\\n\"},{\"insert\":{\"native-image\":{\"alt\":\"图\",\"url\":\"https://i0.hdslb.com/image.png\",\"width\":800,\"height\":450}}},{\"insert\":{\"cut-off\":{\"type\":\"1\",\"url\":\"\"}}},{\"insert\":{\"video-card\":{\"id\":\"av1\",\"url\":\"https://i0.hdslb.com/video.jpg\",\"alt\":\"视频\"}}},{\"insert\":{\"article-card\":{\"id\":\"cv2\",\"url\":\"https://i0.hdslb.com/article.jpg\",\"alt\":\"专栏\"}}},{\"insert\":{\"vote-card\":{\"id\":\"3\",\"url\":\"https://i0.hdslb.com/vote.jpg\",\"alt\":\"投票\"}}},{\"insert\":{\"live-card\":{\"id\":\"lv4\",\"url\":\"https://i0.hdslb.com/live.jpg\",\"alt\":\"直播\"}}}]}",
  "opus": null
}
```

- [ ] **Step 2: Add failing Delta tests**

Add assertions for exact block order and link construction:

```csharp
[TestMethod]
public void Parse_DeltaJson_PreservesFormattingAndEmbeds()
{
    var json = File.ReadAllText(Path.Combine("Fixtures", "type3.json"));
    var article = JsonConvert.DeserializeObject<ArticleDataModel>(json);
    var blocks = new ArticleContentParser().Parse(article).ToList();

    CollectionAssert.AreEqual(
        new[] { ArticleBlockType.Text, ArticleBlockType.Text, ArticleBlockType.Image,
            ArticleBlockType.Separator, ArticleBlockType.Embed, ArticleBlockType.Embed,
            ArticleBlockType.Embed, ArticleBlockType.Embed },
        blocks.Select(item => item.Type).ToArray());
    Assert.AreEqual(ArticleTextKind.Heading, ((ArticleTextBlockModel)blocks[0]).Kind);
    var quote = (ArticleTextBlockModel)blocks[1];
    Assert.AreEqual(ArticleTextKind.Quote, quote.Kind);
    Assert.IsTrue(quote.Inlines.Single().Bold);
    Assert.AreEqual("https://www.bilibili.com/video/av1", quote.Inlines.Single().Link);
    CollectionAssert.AreEqual(
        new[] { ArticleEmbedType.Video, ArticleEmbedType.Article, ArticleEmbedType.Vote, ArticleEmbedType.Live },
        blocks.OfType<ArticleEmbedBlockModel>().Select(item => item.EmbedType).ToArray());
    Assert.AreEqual("https://www.bilibili.com/video/av1", ((ArticleEmbedBlockModel)blocks[4]).Link);
    Assert.AreEqual("https://www.bilibili.com/read/cv2", ((ArticleEmbedBlockModel)blocks[5]).Link);
    Assert.AreEqual("https://t.bilibili.com/vote/h5/index/#/result?vote_id=3", ((ArticleEmbedBlockModel)blocks[6]).Link);
    Assert.AreEqual("https://live.bilibili.com/4", ((ArticleEmbedBlockModel)blocks[7]).Link);
}
```

Add one Opus fallback test with `content="{"`, `para_type=1` text nodes, and `para_type=2` pictures. Assert that invalid Delta produces text and image blocks from `opus.content.paragraphs`. Add one unsupported object insert test and assert it produces `ArticleUnknownBlockModel` instead of throwing.

- [ ] **Step 3: Run the Delta tests and verify failure**

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj --filter "Parse_DeltaJson|Parse_InvalidDelta|Parse_UnknownInsert"
```

Expected: the Delta test fails because the dispatcher does not yet normalize JSON operations.

- [ ] **Step 4: Implement Delta normalization**

Parse `content` with `JObject.Parse`, accept both `attributes` and the documented singular `attribute`, and iterate `ops` in order. Maintain one pending text block:

```text
string insert without newline -> append one inline carrying the operation attributes
newline in string insert      -> finalize the pending block using newline block attributes
native-image object           -> flush pending text, append Image
cut-off object                -> flush pending text, append Separator
known *-card object           -> flush pending text, append Embed with canonical Link
unknown object                -> flush pending text, append Unknown with the property name
end of ops                    -> flush non-empty pending text as Paragraph
```

Map block attributes exactly:

```text
header=1..6        -> Heading and HeadingLevel
blockquote=true   -> Quote
list=bullet        -> Bullet
list=ordered       -> Ordered and increment order within the current list
align              -> Alignment string (left, center, right, justify)
```

Map inline attributes `bold`, `italic`, `strike`, `color`, and `link`. Split on every `\n`; do not keep the newline character in an inline.

- [ ] **Step 5: Implement limited Opus fallback**

Only enter fallback when Delta parsing throws or produces zero blocks. Read `opus.content.paragraphs` defensively:

```text
para_type=1 -> Paragraph or Heading based on word.font_level/font_size; concatenate text.nodes[].word.words
para_type=2 -> one Image per pic.pics[] entry
para_type=6 -> Bullet/Ordered using format.list_format.order and text nodes
other type  -> Unknown with "暂不支持的 Opus 段落类型：{para_type}"
```

Do not throw for a malformed individual paragraph. Append an `Unknown` block, continue, and let the VM log the article-level parsing result.

- [ ] **Step 6: Run all pure parser tests**

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj --configuration Release
```

Expected: all tests pass with zero failures.

- [ ] **Step 7: Commit JSON parsing after explicit Git confirmation**

Suggested signed commit:

```text
支持新版专栏正文内容块

- 解析 Delta 文本样式、图片、分隔线和嵌入卡片
- 使用 Opus 段落补缺并为未知节点提供降级块

Co-Authored-By: Codex <noreply@openai.com>
```

### Task 4: Add the Article API and ViewModel

**Files:**

- Create: `BiliBili.UWP/Api/ArticleAPI.cs`
- Create: `BiliBili.UWP/Modules/ArticleVM.cs`
- Modify: `BiliBili.UWP/BiliBili.UWP.csproj`

- [ ] **Step 1: Add the API wrapper**

Implement the request without WBI or app signing:

```csharp
namespace BiliBili.UWP.Api
{
    public class ArticleAPI
    {
        public ApiModel View(long articleId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/article/view",
                parameter = "id=" + articleId,
                headers = ApiUtils.GetDefaultHeaders()
            };
        }
    }
}
```

- [ ] **Step 2: Implement VM state and stale-request protection**

`ArticleVM : IModules` owns an `ArticleAPI`, an `ArticleContentParser`, and an integer load version. Expose these notifying properties:

```csharp
public bool Loading { get; private set; }
public string ErrorMessage { get; private set; }
public ArticleDataModel Article { get; private set; }
public IReadOnlyList<ArticleBlockModel> Blocks { get; private set; }
public long ArticleId { get; private set; }
```

Implement `Task LoadAsync(long articleId)` in this order:

```text
increment loadVersion and capture it locally
set ArticleId, Loading=true, ErrorMessage=null, Article=null, Blocks=empty
await new ArticleAPI().View(articleId).Request()
return immediately if captured version is no longer current
if HttpResults.status is false, set ErrorMessage to response.message
deserialize ApiDataModel<ArticleDataModel>
if envelope is null, set "专栏数据解析失败"
if envelope.code is nonzero, set envelope.message or "专栏加载失败（{code}）"
parse blocks; if no blocks, set "专栏正文为空"
if blocks contain Unknown entries, write one INFO log containing the article ID and unknown count
publish Article and Blocks together
catch network/other exception and use HandelError(ex).message
in finally, set Loading=false only when this request is still current
```

Use one private `SetState` helper or explicit setters; every changed property must call `DoPropertyChanged` with the exact property name. Log parser exceptions with `LogHelper.WriteLog("专栏内容解析失败", LogType.ERROR, ex)` before setting the page error.

- [ ] **Step 3: Add project includes**

Add:

```xml
<Compile Include="Api\ArticleAPI.cs" />
<Compile Include="Modules\ArticleVM.cs" />
```

- [ ] **Step 4: Perform API contract checks**

Verify literals without sending a request:

```powershell
rg -n "x/article/view|parameter = \"id=\"|GetDefaultHeaders|useWbi" BiliBili.UWP\Api\ArticleAPI.cs
```

Expected: endpoint, `id=` parameter, and default headers are present; `useWbi` is absent.

Then perform one read-only live check:

```powershell
$r = Invoke-RestMethod 'https://api.bilibili.com/x/article/view?id=1' -Headers @{ 'User-Agent'='Mozilla/5.0'; 'Referer'='https://www.bilibili.com/read/cv1' }
$r.code
$r.data.type
```

Expected under a non-risk-controlled connection: `0` and `0`. Treat `-352` or `-509` as environment/API evidence, not a source-code test failure.

### Task 5: Render Native Rich Text and Content Blocks

**Files:**

- Create: `BiliBili.UWP/Controls/ArticleTextBlockControl.xaml`
- Create: `BiliBili.UWP/Controls/ArticleTextBlockControl.xaml.cs`
- Modify: `BiliBili.UWP/Pages/FindMore/ArticleContentPage.xaml`
- Modify: `BiliBili.UWP/Pages/FindMore/ArticleContentPage.xaml.cs`
- Modify: `BiliBili.UWP/BiliBili.UWP.csproj`

- [ ] **Step 1: Create the text renderer control**

The XAML contains one `RichTextBlock` with text selection and wrapping enabled:

```xml
<UserControl
    x:Class="BiliBili.UWP.Controls.ArticleTextBlockControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <RichTextBlock x:Name="richText"
                   IsTextSelectionEnabled="True"
                   TextWrapping="Wrap"
                   LineHeight="28" />
</UserControl>
```

In code-behind, register an `ArticleTextBlockModel` dependency property. On change, clear `richText.Blocks`, create one `Paragraph`, set heading font sizes (`1=30`, `2=24`, `3=21`, `4-6=18`), quote left indent, list marker text, and alignment. For each inline:

- create `Run` for unlinked text;
- create `Hyperlink` for linked text and raise `LinkClicked(this, link)` on click;
- apply `FontWeight`, `FontStyle`, `TextDecorations`, and a parsed solid-color brush;
- if a color cannot be parsed, inherit the theme foreground.

Do not use `XamlReader`; all elements are constructed through typed UWP APIs.

- [ ] **Step 2: Replace the WebView page XAML**

Keep the existing 48-pixel top bar and replace the content row with:

```xml
<Grid Grid.Row="1">
    <ScrollViewer x:Name="articleScroll" Visibility="Collapsed">
        <StackPanel x:Name="articleRoot" MaxWidth="760" Margin="42,28,42,48">
            <TextBlock x:Name="articleTitle" FontSize="28" FontWeight="SemiBold"
                       TextWrapping="Wrap" />
            <Grid Margin="0,18,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Ellipse Width="38" Height="38">
                    <Ellipse.Fill><ImageBrush x:Name="authorAvatar" Stretch="UniformToFill" /></Ellipse.Fill>
                </Ellipse>
                <StackPanel Grid.Column="1" Margin="10,0,0,0">
                    <TextBlock x:Name="authorName" FontWeight="SemiBold" />
                    <TextBlock x:Name="articleMeta" FontSize="12" Foreground="Gray" />
                </StackPanel>
            </Grid>
            <TextBlock x:Name="articleStats" FontSize="12" Foreground="Gray" Margin="0,0,0,20" />
            <ItemsControl x:Name="articleBlocks" ItemTemplateSelector="{StaticResource ArticleBlockSelector}" />
        </StackPanel>
    </ScrollViewer>
    <StackPanel x:Name="errorPanel" Visibility="Collapsed" HorizontalAlignment="Center"
                VerticalAlignment="Center" MaxWidth="420" Margin="24">
        <TextBlock x:Name="errorText" TextWrapping="Wrap" TextAlignment="Center" />
        <Button Content="重试" Click="Retry_Click" HorizontalAlignment="Center" Margin="0,16,0,0" />
    </StackPanel>
    <ProgressRing x:Name="pr_Load" Width="56" Height="56" />
</Grid>
```

Add page resources for five templates. Define `ArticleBlockTemplateSelector : DataTemplateSelector` at the bottom of `ArticleContentPage.xaml.cs`, expose one `DataTemplate` property per block type, and switch on `ArticleBlockModel.Type`:

```text
Text      -> ArticleTextBlockControl, LinkClicked bound to page handler
Image     -> Image with Stretch=Uniform and ImageFailed handler plus alt fallback
Separator -> 1px Border using theme divider color
Embed     -> Button with 104x62 cover, title, type label, and Click handler
Unknown   -> muted TextBlock with Description
```

Use `VisualStateManager` with `AdaptiveTrigger MinWindowWidth="700"`. Wide state keeps `Margin="42,28,42,48"` and title size 28; narrow state uses `Margin="16,20,16,40"`, title size 23, and moves statistics below author metadata. Do not scale font size continuously with window width.

- [ ] **Step 3: Replace WebView code-behind with native loading**

Remove WebView2 fields, initialization, navigation events, cookie synchronization, DOM script, and `CoreWebView2` imports. Keep `NavigationCacheMode.Required`.

On navigation:

```csharp
protected override async void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    if (!ArticleParameterParser.TryParse(e.Parameter, out articleId))
    {
        ShowError("无法打开无效的专栏地址");
        return;
    }
    await LoadArticleAsync();
}
```

`LoadArticleAsync()` awaits `viewModel.LoadAsync(articleId)`, then:

- shows only the progress ring while loading;
- shows only `errorPanel` when `ErrorMessage` is non-empty;
- assigns title, avatar, `Utils.TimestampToDatetime(publish_time).ToString("yyyy-MM-dd")`, category, and compact statistics;
- sets `articleBlocks.ItemsSource = viewModel.Blocks` and shows the scroll viewer;
- scrolls to the top with `articleScroll.ChangeView(null, 0, null, true)` after successful navigation.

- [ ] **Step 4: Implement link, card, image, retry, and share behavior**

Use one `OpenLinkAsync(string link)` helper:

```text
empty/invalid URI -> show "无法打开无效链接"
MessageCenter.HandelUrl(link) returns true -> stop
HTTP/HTTPS -> ask "是否调用外部浏览器打开此链接？" and launch only on confirmation
other scheme -> show "不支持打开的链接：{link}"
```

The text-control event and embed-card button both call this helper. The image failure handler hides only the failed `Image` and reveals its adjacent alt text. Retry calls `LoadArticleAsync()` with the stored ID.

Share no longer reads `web.Source`. Copy exactly:

```csharp
var url = "https://www.bilibili.com/read/cv" + articleId;
var package = new DataPackage();
package.SetText(url);
Clipboard.SetContent(package);
Clipboard.Flush();
Utils.ShowMessageToast("已将地址复制到剪切板", 3000);
```

- [ ] **Step 5: Add project entries**

Add the control code-behind beside other control compile entries and the XAML beside other page entries:

```xml
<Compile Include="Controls\ArticleTextBlockControl.xaml.cs">
  <DependentUpon>ArticleTextBlockControl.xaml</DependentUpon>
</Compile>
<Page Include="Controls\ArticleTextBlockControl.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
```

Ensure the previously added API, model, and module entries occur exactly once.

- [ ] **Step 6: Validate source removal and XML structure**

Run:

```powershell
rg -n "WebView2|CoreWebView2|WebView2CookieHelper|ExecuteScriptAsync" BiliBili.UWP\Pages\FindMore\ArticleContentPage.*
[xml](Get-Content -Raw BiliBili.UWP\Pages\FindMore\ArticleContentPage.xaml) | Out-Null
[xml](Get-Content -Raw BiliBili.UWP\Controls\ArticleTextBlockControl.xaml) | Out-Null
[xml](Get-Content -Raw BiliBili.UWP\BiliBili.UWP.csproj) | Out-Null
```

Expected: `rg` returns no matches for the page; all three XML parses complete without an exception.

- [ ] **Step 7: Commit native page integration after explicit Git confirmation**

Suggested signed commit:

```text
将专栏阅读页改为原生界面

- 通过正文 API 加载文章并处理过期请求与错误状态
- 使用原生 XAML 渲染文章信息、富文本、图片和嵌入卡片
- 保留站内导航、外链确认、重试和规范地址分享

Co-Authored-By: Codex <noreply@openai.com>
```

### Task 6: Run Automated and Static Verification

**Files:**

- Inspect: all paths changed in Tasks 1-5

- [ ] **Step 1: Run parser tests from a clean invocation**

Run:

```powershell
dotnet test tools\ArticleParserTests\ArticleParserTests.csproj --configuration Release --no-restore
```

Expected: zero failed tests. If packages were not previously restored, omit `--no-restore` once, then rerun with it.

- [ ] **Step 2: Check fixtures and project entries**

Run:

```powershell
Get-Content -Raw tools\ArticleParserTests\Fixtures\type0.json | ConvertFrom-Json | Out-Null
Get-Content -Raw tools\ArticleParserTests\Fixtures\type3.json | ConvertFrom-Json | Out-Null
rg -n "ArticleAPI.cs|ArticleModels.cs|ArticleParameterParser.cs|ArticleContentParser.cs|ArticleVM.cs|ArticleTextBlockControl" BiliBili.UWP\BiliBili.UWP.csproj
```

Expected: both fixtures parse, and every production file appears exactly once in the project.

- [ ] **Step 3: Run repository checks**

Run:

```powershell
git diff --check
git status --short
git diff --stat
```

Expected: no whitespace errors; only the explicitly authorized implementation and test files are changed.

- [ ] **Step 4: Build the main UWP application with Visual Studio MSBuild**

Locate MSBuild:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$install = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $install 'MSBuild\Current\Bin\MSBuild.exe'
& $msbuild BiliBili.UWP\BiliBili.UWP.csproj /restore /t:Build /p:Configuration=Debug /p:Platform=x64 /p:UseSharedCompilation=false /m
```

Expected: `BiliBili.UWP` compiles successfully. If only AppX signing or packaging fails because `BiliBili.UWP_TemporaryKey.pfx` is unavailable, report compilation and packaging separately and do not rerun the build solely for that packaging failure.

### Task 7: Perform Runtime Validation

**Files:**

- Inspect at runtime: `BiliBili.UWP/Pages/FindMore/ArticleContentPage.xaml`

- [ ] **Step 1: Deploy `Debug|x64` from Visual Studio**

Use a machine with Windows SDK 10.0.19041.0 and a local test certificate. Start the UWP application and open专栏 from both `SearchV2Page` and the专栏 list so numeric and URL navigation paths are exercised.

- [ ] **Step 2: Validate legacy and current bodies**

Open `cv1` and confirm headings, paragraphs, links, and long-text scrolling. Open one confirmed `type=3` article and confirm text styles, images, separators, and all available embed cards preserve API order.

- [ ] **Step 3: Validate responsive and failure states**

Resize across 700 pixels and confirm margins/title/metadata change without overlap. Verify one failed image leaves alt text, an invalid ID shows a retryable error, and a risk-control response such as `-509` displays its API message rather than “解析失败”.

- [ ] **Step 4: Validate navigation and sharing**

Confirm video, article, and live cards use native navigation where supported; an ordinary external URL requires confirmation; an unsupported scheme is blocked. Share must copy `https://www.bilibili.com/read/cv{id}` for the currently displayed article.

- [ ] **Step 5: Record results without overstating coverage**

Report parser-test count, XML/static checks, exact MSBuild result, packaging/signing result, and which runtime cases were manually observed. If deployment or runtime verification is unavailable, state that explicitly and leave those checks outstanding.
