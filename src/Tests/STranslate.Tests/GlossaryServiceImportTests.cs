using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Tests;

public class GlossaryServiceImportTests
{
    private readonly record struct Seed(string Source, string Target);

    private static GlossaryService CreateService(params Seed[] entries)
    {
        var settings = new GlossarySettings();
        foreach (var entry in entries)
            settings.Entries.Add(new GlossaryEntry(entry.Source, entry.Target));

        // 未挂 AppStorage 时 Save() 是空操作，正好用于纯内存测试。
        return new GlossaryService(settings);
    }

    [Fact]
    public void Import_AppendsNewEntries()
    {
        var service = CreateService();

        var added = service.Import([new GlossaryEntry("盖子", "cap"), new GlossaryEntry("模具", "mold")]);

        Assert.Equal(2, added);
        Assert.Equal(2, service.Entries.Count);
        Assert.Equal("盖子", service.Entries[0].Source);
        Assert.Equal("mold", service.Entries[1].Target);
    }

    [Fact]
    public void Import_SkipsEntriesAlreadyPresent()
    {
        var service = CreateService(new Seed("盖子", "cap"));

        var added = service.Import([new GlossaryEntry("盖子", "cap"), new GlossaryEntry("模具", "mold")]);

        Assert.Equal(1, added);
        Assert.Equal(2, service.Entries.Count);
    }

    [Fact]
    public void Import_SkipsDuplicatesInsideTheSameBatch()
    {
        var service = CreateService();

        var added = service.Import([new GlossaryEntry("盖子", "cap"), new GlossaryEntry("盖子", "cap")]);

        Assert.Equal(1, added);
        Assert.Single(service.Entries);
    }

    [Fact]
    public void Import_IgnoresEntriesWithBlankSide()
    {
        var service = CreateService();

        var added = service.Import(
        [
            new GlossaryEntry("  ", "cap"),
            new GlossaryEntry("模具", "   "),
            new GlossaryEntry("锌合金", "zamac"),
        ]);

        Assert.Equal(1, added);
        Assert.Single(service.Entries);
        Assert.Equal("锌合金", service.Entries[0].Source);
    }

    [Fact]
    public void Import_TrimsValues()
    {
        var service = CreateService();

        service.Import([new GlossaryEntry("  盖子  ", "  cap  ")]);

        Assert.Single(service.Entries);
        Assert.Equal("盖子", service.Entries[0].Source);
        Assert.Equal("cap", service.Entries[0].Target);
    }

    [Fact]
    public void Import_TreatsTrimmedValuesAsTheSameEntry()
    {
        var service = CreateService(new Seed("盖子", "cap"));

        var added = service.Import([new GlossaryEntry("  盖子  ", " cap ")]);

        Assert.Equal(0, added);
        Assert.Single(service.Entries);
    }

    [Fact]
    public void Clear_RemovesEveryEntry()
    {
        var service = CreateService(new Seed("盖子", "cap"), new Seed("模具", "mold"));

        service.Clear();

        Assert.Empty(service.Entries);
        Assert.False(service.HasEntries);
    }

    [Fact]
    public void GetEffectiveEntries_FiltersAndTrims()
    {
        var service = CreateService(
            new Seed("  盖子  ", " cap "),
            new Seed("  ", "mold"));

        var effective = service.GetEffectiveEntries();

        Assert.Single(effective);
        Assert.Equal("盖子", effective[0].Source);
        Assert.Equal("cap", effective[0].Target);
    }

    [Fact]
    public void GetEffectiveEntries_ReturnsCopies()
    {
        var service = CreateService(new Seed("盖子", "cap"));

        var effective = service.GetEffectiveEntries();
        effective[0].Source = "改过";

        Assert.Equal("盖子", service.Entries[0].Source);
    }
}
