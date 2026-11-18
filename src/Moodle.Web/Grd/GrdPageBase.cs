using Microsoft.AspNetCore.Components;

namespace Moodle.Web.Grd;

/// <summary>
/// GRD sayfalarının ortak yükleme, meşgul, hata ve yeniden deneme davranışı (CrsPageBase deseni).
/// Tarayıcı HTTP'si yalnızca etkileşimli render'dan sonra kullanılabildiği için yükleme
/// <see cref="OnAfterRenderAsync"/> içinde başlar.
/// </summary>
public abstract class GrdPageBase : ComponentBase, IDisposable
{
    [Inject] protected GrdApiClient Api { get; set; } = default!;
    private readonly CancellationTokenSource _lifetime = new();
    protected GrdPageBase() => Token = _lifetime.Token;
    private bool _needsLoad = true;
    private bool _hasData;
    private bool _disposed;
    protected CancellationToken Token { get; }
    protected bool Busy { get; private set; }
    protected bool HasData => _hasData && !_needsLoad;
    protected bool Loading => !HasData && (_needsLoad || Busy);
    protected bool CanAct => HasData && !Busy && Error?.Code != "auth_not_ready";
    protected GrdApiException? Error { get; private set; }
    protected string? Success { get; set; }

    protected abstract Task LoadAsync();

    protected void RequestLoad()
    {
        _needsLoad = true;
        _hasData = false;
        Error = null;
        Success = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLoad && !Busy)
        {
            _needsLoad = false;
            await RefreshAsync();
        }
    }

    protected Task RefreshAsync() => ExecuteAsync(async () =>
    {
        await LoadAsync();
        _hasData = true;
    });

    protected async Task ExecuteAsync(Func<Task> action)
    {
        if (Busy || _disposed) return;
        Busy = true;
        Error = null;
        Success = null;
        try { await action(); }
        catch (GrdApiException ex)
        {
            Error = ex;
            if (ex.Code == "auth_not_ready" || ex.Status == System.Net.HttpStatusCode.Unauthorized)
                _hasData = false;
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested) { }
        finally
        {
            Busy = false;
            if (!_disposed) StateHasChanged();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
