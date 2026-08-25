using Cueless.Application.Playback;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Cueless.Web.Components;

public sealed partial class ConcealedPlayer : ComponentBase, IMediaPlayer, IAsyncDisposable
{
    private readonly string frameElementIdentifier = $"concealed-player-{Guid.NewGuid():N}";

    private IJSInProcessObjectReference? shim;
    private DotNetObjectReference<ConcealedPlayer>? self;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    [Parameter]
    public EventCallback Ready { get; set; }

    public PlaybackState State { get; private set; } = PlaybackState.Unstarted;

    public TimeSpan Position =>
        shim is null ? TimeSpan.Zero : TimeSpan.FromSeconds(shim.Invoke<double>("getCurrentTime"));

    public void Cue(string videoIdentifier) => shim?.InvokeVoid("cue", videoIdentifier);

    public void Seek(TimeSpan position) => shim?.InvokeVoid("seek", position.TotalSeconds);

    public void Play() => shim?.InvokeVoid("play");

    public void Pause() => shim?.InvokeVoid("pause");

    [JSInvokable]
    public void OnStateChanged(int state)
    {
        State = state switch
        {
            -1 => PlaybackState.Unstarted,
            0 => PlaybackState.Ended,
            1 => PlaybackState.Playing,
            2 => PlaybackState.Paused,
            3 => PlaybackState.Buffering,
            5 => PlaybackState.Cued,
            _ => State,
        };
    }

    [JSInvokable]
    public void OnError(int code)
    {
        _ = code;
        State = PlaybackState.Unavailable;
    }

    public async ValueTask DisposeAsync()
    {
        self?.Dispose();

        if (shim is not null)
        {
            await shim.DisposeAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var module = await JavaScript.InvokeAsync<IJSObjectReference>(
            "import",
            "./Components/ConcealedPlayer.razor.js");

        shim = (IJSInProcessObjectReference)module;
        self = DotNetObjectReference.Create(this);

        await shim.InvokeVoidAsync("loadPlayer", frameElementIdentifier, self);

        await Ready.InvokeAsync();
    }
}
