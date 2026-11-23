using STranslate.Plugin;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STranslate.Helpers;

/// <summary>
/// 术语库导入导出。文件格式为 JSON 数组，元素形如
/// <c>{ "SourceText": "盖子", "TargetText": "cap" }</c>。
/// </summary>
public static class GlossaryFileHelper
{
    /// <summary>
    /// 统一的输出编码（UTF-8 with BOM），与历史记录导出保持一致。
    /// </summary>
    public static readonly Encoding Utf8BomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>
    /// 把术语条目序列化成导入格式的 JSON 文本。
    /// 只导出原文与译文都已填写的条目，顺序与界面上一致。
    /// </summary>
    public static string Serialize(IEnumerable<GlossaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var items = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Source) &&
                            !string.IsNullOrWhiteSpace(entry.Target))
            .Select(entry => new GlossaryFileItem
            {
                SourceText = entry.Source.Trim(),
                TargetText = entry.Target.Trim()
            })
            .ToList();

        return JsonSerializer.Serialize(items, WriteOptions);
    }

    /// <summary>
    /// 解析导入文件内容。允许 UTF-8 BOM、注释与尾随逗号。
    /// 原文或译文为空的条目会被丢弃。
    /// </summary>
    /// <returns>解析出的术语条目；内容不是合法的 JSON 数组时返回 null。</returns>
    public static List<GlossaryEntry>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        // 从文件或剪贴板拿到的文本可能残留 BOM，先去掉再解析。
        var content = json.TrimStart('\uFEFF');

        List<GlossaryFileItem?>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<GlossaryFileItem?>>(content, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (items == null)
            return null;

        var result = new List<GlossaryEntry>(items.Count);
        foreach (var item in items)
        {
            var source = item?.SourceText?.Trim();
            var target = item?.TargetText?.Trim();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                continue;

            result.Add(new GlossaryEntry(source, target));
        }

        return result;
    }
}

/// <summary>
/// 术语库文件中的单条术语。属性名与外部格式保持一致。
/// </summary>
internal sealed class GlossaryFileItem
{
    [JsonPropertyName("SourceText")]
    public string? SourceText { get; set; }

    [JsonPropertyName("TargetText")]
    public string? TargetText { get; set; }
}
