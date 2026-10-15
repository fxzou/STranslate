using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Tests;

/// <summary>
/// 常用语言配置（主界面语言选择框的排序与启停）相关测试
/// </summary>
public class CommonLanguagesTests
{
    private static readonly LangEnum[] FullLanguages =
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Japanese,
        LangEnum.Korean,
        LangEnum.French,
        LangEnum.German,
    ];

    private static readonly LangEnum[] SimpleModeLanguages =
    [
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Japanese,
        LangEnum.Korean,
        LangEnum.French,
    ];

    [Fact]
    public void ResolveVisibleLanguageOrder_PutsAutoFirstAndFollowsConfiguredOrder()
    {
        var order = DataProvider.ResolveVisibleLanguageOrder(
            FullLanguages,
            [LangEnum.Korean, LangEnum.English],
            LangEnum.Auto);

        Assert.Equal(new[] { LangEnum.Auto, LangEnum.Korean, LangEnum.English }, order);
    }

    [Fact]
    public void ResolveVisibleLanguageOrder_KeepsSelectedLanguageNotInCommonLanguages()
    {
        var order = DataProvider.ResolveVisibleLanguageOrder(
            FullLanguages,
            [LangEnum.English],
            LangEnum.German);

        Assert.Equal(new[] { LangEnum.Auto, LangEnum.English, LangEnum.German }, order);
    }

    [Fact]
    public void ResolveVisibleLanguageOrder_DoesNotDuplicateSelectedCommonLanguage()
    {
        var order = DataProvider.ResolveVisibleLanguageOrder(
            FullLanguages,
            [LangEnum.English, LangEnum.Korean],
            LangEnum.Korean);

        Assert.Equal(new[] { LangEnum.Auto, LangEnum.English, LangEnum.Korean }, order);
    }

    [Fact]
    public void ResolveVisibleLanguageOrder_SkipsLanguagesMissingFromSource()
    {
        // 简化语言模式的目标语种不含 Auto
        var order = DataProvider.ResolveVisibleLanguageOrder(
            SimpleModeLanguages,
            [LangEnum.English, LangEnum.Korean],
            LangEnum.French);

        Assert.Equal(new[] { LangEnum.English, LangEnum.Korean, LangEnum.French }, order);
    }

    [Fact]
    public void ResolveVisibleLanguageOrder_FallsBackToSelectedWhenNoCommonLanguages()
    {
        var order = DataProvider.ResolveVisibleLanguageOrder(FullLanguages, [], LangEnum.Japanese);

        Assert.Equal(new[] { LangEnum.Auto, LangEnum.Japanese }, order);
    }

    [Fact]
    public void ApplyCommonLanguages_RemovesAutoAndDuplicatesAndPreservesOrder()
    {
        var settings = new Settings();

        settings.ApplyCommonLanguages([LangEnum.English, LangEnum.Auto, LangEnum.Korean, LangEnum.English]);

        Assert.Equal(new[] { LangEnum.English, LangEnum.Korean }, settings.CommonLanguages);
    }

    [Fact]
    public void ApplyCommonLanguages_KeepsDefaultValueWhenUnchanged()
    {
        var settings = new Settings();
        var original = settings.CommonLanguages;

        settings.ApplyCommonLanguages(original);

        Assert.Same(original, settings.CommonLanguages);
    }

    [Fact]
    public void CommonLanguages_DefaultValueExcludesAuto()
    {
        var settings = new Settings();

        Assert.NotEmpty(settings.CommonLanguages);
        Assert.DoesNotContain(LangEnum.Auto, settings.CommonLanguages);
        Assert.False(settings.ShowAllLanguages);
    }
}
