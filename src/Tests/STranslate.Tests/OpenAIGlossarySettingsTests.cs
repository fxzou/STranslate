using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;

namespace STranslate.Tests;

public class OpenAIGlossarySettingsTests
{
    [Fact]
    public void SwitchReadsTheServiceLevelOption()
    {
        var context = System.Reflection.DispatchProxy.Create<IPluginContext, GlossaryProxy>();
        var proxy = (GlossaryProxy)(object)context;
        proxy.UseGlossary = true;

        using var vm = new STranslate.Plugin.Translate.OpenAI.ViewModel.SettingsViewModel(context, new Settings(), new Main());

        Assert.True(vm.UseGlossary);
    }

    [Fact]
    public void SwitchWritesBackToTheServiceLevelOption()
    {
        var context = System.Reflection.DispatchProxy.Create<IPluginContext, GlossaryProxy>();
        var proxy = (GlossaryProxy)(object)context;

        using var vm = new STranslate.Plugin.Translate.OpenAI.ViewModel.SettingsViewModel(context, new Settings(), new Main())
        {
            UseGlossary = true
        };

        Assert.True(proxy.UseGlossary);
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public void SwitchDoesNotTouchThePluginStorage()
    {
        // 术语库开关属于服务级配置，不应写进插件自己的设置文件。
        var context = System.Reflection.DispatchProxy.Create<IPluginContext, GlossaryProxy>();
        var proxy = (GlossaryProxy)(object)context;

        using var vm = new STranslate.Plugin.Translate.OpenAI.ViewModel.SettingsViewModel(context, new Settings(), new Main())
        {
            UseGlossary = true
        };

        Assert.Equal(0, proxy.Saves);
    }

    public class GlossaryProxy : System.Reflection.DispatchProxy
    {
        public bool UseGlossary;
        public int Writes;
        public int Saves;

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "get_UseGlossary":
                    return UseGlossary;
                case "set_UseGlossary":
                    UseGlossary = (bool)(args![0] ?? false);
                    Writes++;
                    return null;
                case "SaveSettingStorage":
                    Saves++;
                    return null;
                default:
                    return null;
            }
        }
    }
}
