using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;

namespace STranslate.Tests;

public class LangDetectOptionsTests
{
    [Fact]
    public void Record_Carries_All_Configured_Values()
    {
        var options = new LangDetectOptions(
            LanguageDetectorType.Bing,
            0.65,
            LangEnum.Japanese,
            LangEnum.English,
            LangEnum.ChineseSimplified)
        {
            SameLanguageTargetLang = LangEnum.Korean,
        };

        Assert.Equal(LanguageDetectorType.Bing, options.Detector);
        Assert.Equal(0.65, options.LocalDetectorRate);
        Assert.Equal(LangEnum.Japanese, options.SourceLangIfAuto);
        Assert.Equal(LangEnum.English, options.FirstLanguage);
        Assert.Equal(LangEnum.ChineseSimplified, options.SecondLanguage);
        Assert.Equal(LangEnum.Korean, options.SameLanguageTargetLang);
    }

    [Fact]
    public void SameLanguageTargetLang_Defaults_To_Null()
    {
        var options = CreateOptions();

        Assert.Null(options.SameLanguageTargetLang);
    }

    [Fact]
    public void Records_With_Same_Values_Are_Equal()
    {
        var a = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English);
        var b = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Configured_Target_Takes_Precedence_Over_Same_Language_Rule()
    {
        var options = CreateOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.French, options.ResolveTargetLanguage(LangEnum.English, LangEnum.French));
    }

    [Fact]
    public void Auto_Target_Uses_Second_Language_When_Source_Is_First_Language()
    {
        var options = CreateOptions();

        Assert.Equal(LangEnum.English, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.Auto));
    }

    [Fact]
    public void Auto_Target_Uses_Second_Language_When_Source_Is_Chinese_Traditional()
    {
        var options = CreateOptions();

        Assert.Equal(LangEnum.English, options.ResolveTargetLanguage(LangEnum.ChineseTraditional, LangEnum.Auto));
    }

    [Fact]
    public void Auto_Target_Uses_First_Language_When_Source_Is_Other_Language()
    {
        var options = CreateOptions();

        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.Japanese, LangEnum.Auto));
    }

    [Fact]
    public void Identical_Source_And_Target_Is_Replaced_By_Configured_Language()
    {
        var options = CreateOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.English, LangEnum.English));
    }

    [Fact]
    public void Auto_Resolved_Target_Equal_To_Source_Is_Replaced()
    {
        // 第一、第二语言均为英语时，「源语种英语 + 目标自动」会解析出与源语种相同的目标，同样应触发替换
        var options = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.English, LangEnum.English)
        {
            SameLanguageTargetLang = LangEnum.Japanese,
        };

        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.English, LangEnum.Auto));
    }

    [Fact]
    public void Identical_Language_With_Auto_Setting_Keeps_Source()
    {
        var options = CreateOptions(LangEnum.Auto);

        Assert.Equal(LangEnum.English, options.ResolveTargetLanguage(LangEnum.English, LangEnum.English));
    }

    [Fact]
    public void Identical_Language_With_Null_Setting_Keeps_Source()
    {
        // 图片翻译链路不设置 SameLanguageTargetLang，必须保持既有行为
        var options = CreateOptions();

        Assert.Equal(LangEnum.English, options.ResolveTargetLanguage(LangEnum.English, LangEnum.English));
    }

    [Fact]
    public void Simple_Mode_Defaults_To_Disabled_And_English()
    {
        var options = CreateOptions();

        Assert.False(options.SimpleLanguageMode);
        Assert.Equal(LangEnum.English, options.SimpleLanguageTargetLang);
    }

    [Fact]
    public void Simple_Mode_Translates_Chinese_Into_Configured_Language()
    {
        var options = CreateSimpleOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Translates_Chinese_Traditional_Into_Configured_Language()
    {
        var options = CreateSimpleOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.ChineseTraditional, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Translates_Cantonese_Into_Configured_Language()
    {
        var options = CreateSimpleOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.Cantonese, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Translates_Other_Language_Into_Chinese_Simplified()
    {
        var options = CreateSimpleOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.Japanese, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Ignores_Configured_Target_Language()
    {
        // 简化模式下目标语种完全由模式决定，调用方的目标语种配置不再参与
        var options = CreateSimpleOptions(LangEnum.Japanese);

        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.English, LangEnum.French));
        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.French));
    }

    [Fact]
    public void Simple_Mode_Ignores_First_And_Second_Language_Settings()
    {
        // 第一/第二语言在本模式下不参与推导
        var options = new LangDetectOptions(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.English, LangEnum.Korean)
        {
            SimpleLanguageMode = true,
            SimpleLanguageTargetLang = LangEnum.Japanese,
        };

        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.English, LangEnum.Auto));
        Assert.Equal(LangEnum.Japanese, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Chinese_Target_Equal_To_Source_Keeps_Source()
    {
        // 中文目标恰好也是中文时源与目标相同，由调用方按原文直出
        var options = CreateSimpleOptions(LangEnum.ChineseSimplified);

        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.Auto));
    }

    [Fact]
    public void Simple_Mode_Disabled_Keeps_Normal_Resolution()
    {
        var options = CreateSimpleOptions(LangEnum.Japanese, enabled: false);

        Assert.Equal(LangEnum.English, options.ResolveTargetLanguage(LangEnum.ChineseSimplified, LangEnum.Auto));
        Assert.Equal(LangEnum.ChineseSimplified, options.ResolveTargetLanguage(LangEnum.Japanese, LangEnum.Auto));
    }

    private static LangDetectOptions CreateOptions(LangEnum? sameLanguageTarget = null)
        => new(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English)
        {
            SameLanguageTargetLang = sameLanguageTarget,
        };

    private static LangDetectOptions CreateSimpleOptions(LangEnum simpleTarget, bool enabled = true)
        => new(LanguageDetectorType.Local, 0.8, LangEnum.English, LangEnum.ChineseSimplified, LangEnum.English)
        {
            SimpleLanguageMode = enabled,
            SimpleLanguageTargetLang = simpleTarget,
        };
}
