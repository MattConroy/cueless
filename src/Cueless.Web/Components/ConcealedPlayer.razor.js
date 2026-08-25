let player;

export function loadPlayer(elementIdentifier, reference) {
    return new Promise(resolve => {
        // The IFrame API announces itself through one global callback and only fires it
        // once, so a player can be built either now or when that callback arrives.
        const create = () => {
            player = new YT.Player(elementIdentifier, {
                playerVars: {
                    controls: 0,
                    disablekb: 1,
                    fs: 0,
                    modestbranding: 1,
                    playsinline: 1,
                    rel: 0
                },
                events: {
                    onReady: () => resolve(),
                    onStateChange: event => reference.invokeMethod('OnStateChanged', event.data),
                    onError: event => reference.invokeMethod('OnError', event.data)
                }
            });
        };

        if (window.YT && window.YT.Player) {
            create();
        } else {
            window.onYouTubeIframeAPIReady = create;
        }
    });
}

export function cue(videoIdentifier) {
    player.cueVideoById(videoIdentifier);
}

export function seek(seconds) {
    player.seekTo(seconds, true);
}

export function play() {
    player.playVideo();
}

export function pause() {
    player.pauseVideo();
}

export function getCurrentTime() {
    return player.getCurrentTime();
}
