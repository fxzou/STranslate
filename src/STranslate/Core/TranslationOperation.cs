using STranslate.Plugin;

namespace STranslate.Core;

/// <summary>
/// 保存一次翻译执行的不可变输入，并提供仅对本次操作生效的插件执行和结果发布能力。
/// </summary>
internal sealed class TranslationOperation
{
    private readonly TranslationResultCoordinator _coordinator;
    private readonly Func<string, string> _getTranslation;

    internal TranslationOperation(
        TranslationResultCoordinator coordinator,
        Func<string, string> getTranslation,
        long operationId,
        string text,
        LangEnum sourceLang,
        LangEnum targetLang,
        CancellationToken cancellationToken)
    {
        _coordinator = coordinator;
        _getTranslation = getTranslation;
        OperationId = operationId;
        Text = text;
        SourceLang = sourceLang;
        TargetLang = targetLang;
        CancellationToken = cancellationToken;
    }

    internal long OperationId { get; }
    internal string Text { get; }
    internal LangEnum SourceLang { get; }
    internal LangEnum TargetLang { get; }
    internal CancellationToken CancellationToken { get; }
    internal bool IsLatestAutomatic => _coordinator.IsLatestAutomatic(OperationId);

    /// <summary>
    /// 本次操作因源语种与目标语种一致而未真正翻译，展示的是原文。
    /// 此类结果不作为翻译结果写入历史或缓存，避免修改「相同语言目标」后仍命中旧缓存。
    /// </summary>
    internal bool IsPassthrough { get; private set; }

    internal bool TryPrepare(ITranslatePlugin plugin)
    {
        var canPublishMainResult = TryReset(plugin.TransResult);
        TryReset(plugin.TransBackResult);
        return canPublishMainResult;
    }

    internal bool TryPrepareBack(ITranslatePlugin plugin) =>
        TryReset(plugin.TransBackResult);

    internal bool TryPrepare(IDictionaryPlugin plugin) =>
        _coordinator.TryResetDictionary(plugin.DictionaryResult, OperationId);

    internal bool IsCurrent(object channel) =>
        _coordinator.IsCurrent(channel, OperationId);

    internal bool TryPublish(object channel, Action publish) =>
        _coordinator.TryPublish(channel, OperationId, publish);

    internal bool TryPublishAutomatic(Action publish) =>
        _coordinator.TryPublishAutomatic(OperationId, publish);

    internal void ActivateIdentifiedLanguage() =>
        _coordinator.ActivateIdentifiedLanguage(OperationId);

    internal void PublishIdentifiedLanguage(Action publish) =>
        _coordinator.PublishIdentifiedLanguage(OperationId, publish);

    internal void PublishDictionary(DictionaryResult source, DictionaryResult target) =>
        _coordinator.PublishDictionary(source, target, OperationId);

    internal Task<TranslateResult> TranslateAsync(
        ITranslatePlugin plugin,
        LangEnum source,
        LangEnum target) =>
        // 源语种与目标语种一致时无需翻译，直接把原文作为结果发布。
        source == target
            ? PublishSourceTextAsync(plugin.TransResult)
            : ExecuteTranslateAsync(
                plugin,
                new TranslateRequest(Text, source, target),
                plugin.TransResult);

    internal Task<TranslateResult> BackTranslateAsync(
        ITranslatePlugin plugin,
        string text,
        LangEnum source,
        LangEnum target) =>
        ExecuteTranslateAsync(
            plugin,
            new TranslateRequest(text, source, target),
            plugin.TransBackResult);

    internal async Task<DictionaryResult> LookupAsync(IDictionaryPlugin plugin)
    {
        var result = new DictionaryResult();
        if (!IsCurrent(plugin.DictionaryResult))
        {
            result.ResultType = DictionaryResultType.Error;
            return result;
        }

        var startTime = DateTime.Now;
        TryPublish(
            plugin.DictionaryResult,
            () => plugin.DictionaryResult.IsProcessing = true);
        result.IsProcessing = true;
        try
        {
            await plugin.TranslateAsync(Text, result, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            result.ResultType = DictionaryResultType.Error;
            result.Text = _getTranslation("TranslateCancel");
        }
        catch (Exception ex)
        {
            result.ResultType = DictionaryResultType.Error;
            result.Text = $"{_getTranslation("TranslateFail")}: {ex.Message}";
        }
        finally
        {
            result.Duration = result.ResultType == DictionaryResultType.NoResult
                ? TimeSpan.Zero
                : DateTime.Now - startTime;

            result.IsProcessing = false;
            PublishDictionary(result, plugin.DictionaryResult);
        }

        return result;
    }

    /// <summary>
    /// 源语种与目标语种一致时不调用翻译服务，直接把原文发布到可见结果通道。
    /// </summary>
    private Task<TranslateResult> PublishSourceTextAsync(TranslateResult visibleResult)
    {
        IsPassthrough = true;

        var result = new TranslateResult
        {
            IsSuccess = true,
            Text = Text,
            Duration = TimeSpan.Zero,
        };

        if (!IsCurrent(visibleResult))
        {
            result.IsSuccess = false;
            return Task.FromResult(result);
        }

        // 原文即译文：直接写入可见结果通道，无需经过 Forward 的增量变更通知。
        TryPublish(visibleResult, () =>
        {
            visibleResult.IsSuccess = true;
            visibleResult.Text = Text;
            visibleResult.Duration = TimeSpan.Zero;
            visibleResult.IsProcessing = false;
        });

        return Task.FromResult(result);
    }

    private async Task<TranslateResult> ExecuteTranslateAsync(
        ITranslatePlugin plugin,
        TranslateRequest request,
        TranslateResult visibleResult)
    {
        var result = new TranslateResult();
        if (!IsCurrent(visibleResult))
        {
            result.IsSuccess = false;
            return result;
        }

        var startTime = DateTime.Now;
        using var forwarding = Forward(result, visibleResult);
        result.IsProcessing = true;
        try
        {
            await plugin.TranslateAsync(request, result, CancellationToken).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.Text = _getTranslation("TranslateCancel");
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Text = $"{_getTranslation("TranslateFail")}: {ex.Message}";
        }
        finally
        {
            result.Duration = DateTime.Now - startTime;
            result.IsProcessing = false;
        }

        return result;
    }

    private bool TryReset(TranslateResult result) =>
        _coordinator.TryReset(result, OperationId);

    private IDisposable Forward(TranslateResult source, TranslateResult target) =>
        _coordinator.Forward(source, target, OperationId);
}
