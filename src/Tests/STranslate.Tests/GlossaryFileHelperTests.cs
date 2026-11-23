using STranslate.Helpers;
using STranslate.Plugin;

namespace STranslate.Tests;

public class GlossaryFileHelperTests
{
    [Fact]
    public void Serialize_EmitsSourceTextAndTargetText()
    {
        var json = GlossaryFileHelper.Serialize(
        [
            new GlossaryEntry("盖子", "cap"),
            new GlossaryEntry("锌合金", "zamac"),
        ]);

        Assert.Contains("\"SourceText\": \"盖子\"", json);
        Assert.Contains("\"TargetText\": \"cap\"", json);
    }

    [Fact]
    public void Serialize_SkipsEntriesWithBlankSide()
    {
        var json = GlossaryFileHelper.Serialize(
        [
            new GlossaryEntry("盖子", "cap"),
            new GlossaryEntry("  ", "zamac"),
            new GlossaryEntry("模具", ""),
        ]);

        Assert.Contains("盖子", json);
        Assert.DoesNotContain("zamac", json);
        Assert.DoesNotContain("模具", json);
    }

    [Fact]
    public void Serialize_TrimsValues()
    {
        var json = GlossaryFileHelper.Serialize([new GlossaryEntry("  盖子  ", "  cap  ")]);

        Assert.Contains("\"SourceText\": \"盖子\"", json);
        Assert.Contains("\"TargetText\": \"cap\"", json);
    }

    [Fact]
    public void Serialize_OfEmptyCollection_IsEmptyArray()
    {
        var json = GlossaryFileHelper.Serialize([]);

        Assert.Equal("[]", json.Trim());
    }

    [Fact]
    public void Deserialize_ReadsSourceTextAndTargetText()
    {
        const string json = """
            [
              { "SourceText": "盖子", "TargetText": "cap" },
              { "SourceText": "锌合金", "TargetText": "zamac" }
            ]
            """;

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Equal(2, entries.Count);
        Assert.Equal("盖子", entries[0].Source);
        Assert.Equal("cap", entries[0].Target);
        Assert.Equal("锌合金", entries[1].Source);
        Assert.Equal("zamac", entries[1].Target);
    }

    [Fact]
    public void Deserialize_AcceptsUtf8Bom()
    {
        const string json = "\uFEFF[ { \"SourceText\": \"盖子\", \"TargetText\": \"cap\" } ]";

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("盖子", entries[0].Source);
    }

    [Fact]
    public void Deserialize_AcceptsLowercasePropertyNames()
    {
        const string json = """[ { "sourceText": "盖子", "targetText": "cap" } ]""";

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("盖子", entries[0].Source);
    }

    [Fact]
    public void Deserialize_SkipsEntriesWithBlankSide()
    {
        const string json = """
            [
              { "SourceText": "盖子", "TargetText": "cap" },
              { "SourceText": "   ", "TargetText": "zamac" },
              { "SourceText": "模具", "TargetText": "" },
              { "SourceText": "锌合金" }
            ]
            """;

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("盖子", entries[0].Source);
    }

    [Fact]
    public void Deserialize_TrimsValues()
    {
        const string json = """[ { "SourceText": "  盖子  ", "TargetText": "  cap  " } ]""";

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("盖子", entries[0].Source);
        Assert.Equal("cap", entries[0].Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ \"SourceText\": \"盖子\" }")]
    public void Deserialize_ReturnsNullForInvalidContent(string? json)
    {
        Assert.Null(GlossaryFileHelper.Deserialize(json));
    }

    [Fact]
    public void Deserialize_OfEmptyArray_ReturnsEmptyList()
    {
        var entries = GlossaryFileHelper.Deserialize("[]");

        Assert.NotNull(entries);
        Assert.Empty(entries);
    }

    [Fact]
    public void RoundTrip_PreservesEntries()
    {
        var original = new List<GlossaryEntry>
        {
            new("盖子", "cap"),
            new("阶梯中套", "step collar"),
        };

        var entries = GlossaryFileHelper.Deserialize(GlossaryFileHelper.Serialize(original));

        Assert.NotNull(entries);
        Assert.Equal(original.Count, entries.Count);
        for (var index = 0; index < original.Count; index++)
        {
            Assert.Equal(original[index].Source, entries[index].Source);
            Assert.Equal(original[index].Target, entries[index].Target);
        }
    }

    [Fact]
    public void Deserialize_BundledSampleFile_IsSupported()
    {
        // 用户提供的样例格式：带 BOM 的 JSON 数组，属性名为 SourceText / TargetText。
        const string json = """
            [
              { "SourceText": "盖子", "TargetText": "cap" },
              { "SourceText": "锌合金", "TargetText": "zamac" },
              { "SourceText": "凸起", "TargetText": "embossing" },
              { "SourceText": "下凹", "TargetText": "debossing" },
              { "SourceText": "烫金", "TargetText": "hot stamp" },
              { "SourceText": "激光雕刻", "TargetText": "laser engraving" },
              { "SourceText": "丝网印刷", "TargetText": "silk screening" },
              { "SourceText": "电镀", "TargetText": "electroplating" },
              { "SourceText": "内塞", "TargetText": "inner plug" },
              { "SourceText": "中套", "TargetText": "collar" },
              { "SourceText": "模具", "TargetText": "mold" },
              { "SourceText": "批量生产", "TargetText": "mass production" },
              { "SourceText": "植绒", "TargetText": "velvet" },
              { "SourceText": "潘通", "TargetText": "pantone" },
              { "SourceText": "装箱单", "TargetText": "packing list" },
              { "SourceText": "压铸", "TargetText": "Die-cast" },
              { "SourceText": "数控", "TargetText": "CNC" },
              { "SourceText": "喷头", "TargetText": "pump" },
              { "SourceText": "牌子", "TargetText": "plate" },
              { "SourceText": "卡点", "TargetText": "click-system" },
              { "SourceText": "阶梯中套", "TargetText": "step collar" }
            ]
            """;

        var entries = GlossaryFileHelper.Deserialize(json);

        Assert.NotNull(entries);
        Assert.Equal(21, entries.Count);
        Assert.Equal("阶梯中套", entries[^1].Source);
        Assert.Equal("step collar", entries[^1].Target);
    }
}
