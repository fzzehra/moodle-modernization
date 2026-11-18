using Microsoft.AspNetCore.Components;

namespace Moodle.Web.Crs;

public abstract class CrsPageBase : ComponentBase, IDisposable
{
    [Inject] protected CrsApiClient Api { get; set; } = default!;
    private readonly CancellationTokenSource _lifetime = new();
    protected CrsPageBase() => Token = _lifetime.Token;
    private bool _needsLoad = true;
    private bool _hasData;
    private bool _disposed;
    protected CancellationToken Token { get; }
    protected bool Busy { get; private set; }
    protected bool HasData => _hasData && !_needsLoad;
    protected bool Loading => !HasData && (_needsLoad || Busy);
    protected bool CanAct => HasData && !Busy && Error?.Code != "auth_not_ready";
    protected CrsApiException? Error { get; private set; }
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
        // JS/browser HTTP is available only after the interactive render.
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
        catch (CrsApiException ex)
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
