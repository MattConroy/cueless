# Cueless — Build Spec

## Concept

Solo music-guessing game. The player supplies a YouTube playlist built from their
own listening. The application plays a one-second snippet from a random track at a
random offset; the player guesses the title. Wrong or skipped moves to a two-second
snippet from a *different* random offset, then three seconds, then reveal. Score is
the number of guesses taken.

The point is personal recognition, not chart knowledge. Deliberately not Heardle:
snippets come from anywhere in the track rather than the intro, and the pool is the
player's own music.

## Non-goals

- No Spotify integration of any kind. Do not call the Spotify API, do not import
  Spotify data, do not build a Spotify-to-YouTube export tool. Users convert their
  playlists themselves with Soundiiz or TuneMyMusic before using this application.
  This is a hard constraint — the Spotify Developer Policy section three prohibits
  games and any tooling that enables them.
- No multiplayer, no accounts, no server-side persistence.
- No advertisement blocking or stream extraction. Playback goes through the
  official YouTube IFrame Player only.

## Stack

- Blazor WebAssembly, standalone. No hosted server project.
- Static hosting output.
- Mobile-first responsive layout; the primary target is a phone browser.
- Persistence: browser local storage only.

## Conventions

- Clean architecture, applied proportionately — this is a small application and the
  layering should not outweigh the feature set.
- No abbreviations or acronyms in identifiers. Write `YouTubePlaylistRepository`,
  not `YtPlaylistRepo`. Write `configuration`, not `config`. Domain terms that are
  genuinely proper nouns (YouTube, JavaScript) are fine as written.
- No comments unless a piece of code is genuinely non-obvious and cannot be made
  obvious by renaming or restructuring. The buffering and timing workarounds in the
  playback layer are the likely exceptions; everything else should not need them.
- Pull request descriptions and the readme stay short. State what changed and why
  it was needed. Do not narrate the implementation.
- Every pull request gets its own branch off the default branch.
- Each pull request is the smallest complete change that stands on its own —
  buildable, coherent, and reviewable in isolation. Prefer several small ones over
  one that spans a whole build-order step.

### Project layout

```
Cueless.Domain           entities, value objects, game rules — no dependencies
Cueless.Application      use cases, abstractions for playback and persistence
Cueless.Infrastructure   YouTube Data API client, local storage adapter
Cueless.Web              Blazor WebAssembly application, components, interop
```

Domain holds the snippet ladder rules, guess evaluation, and scoring. Application
orchestrates rounds and depends only on abstractions. Infrastructure implements
them. The web project holds components and JavaScript interop and nothing else.

### Components and styling

- One component per screen region, each with its own isolated stylesheet
  (`ComponentName.razor.css`). No global stylesheet beyond resets, typography, and
  custom properties for the palette.
- Shared visual primitives (button, snippet ladder indicator, statistics bar) are
  their own components with their own isolated styles.
- JavaScript is kept to the single unavoidable player shim described under
  Playback, collocated as a component-scoped module and loaded through
  `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)`. No other JavaScript
  in the project, and no global script tags beyond the YouTube IFrame API
  bootstrap. If a feature appears to need more JavaScript, it belongs in C#.

## User flow

1. **Setup** — paste a public or unlisted YouTube playlist address.
2. The application ingests the playlist once and stores the track cache locally.
3. **Play** — random track, snippet ladder, autocomplete guess input.
4. **Reveal** — title, artist, link to the video; running statistics.
5. Return visits skip setup. Multiple saved playlists with switching between them.

## Data layer

### One-time playlist ingest

- `playlistItems.list` for video identifiers and titles.
- `videos.list` with `part=contentDetails,status` for duration and the
  `embeddable` flag.
- Discard anything not embeddable, plus deleted and private items. Report the
  number discarded to the user.
- Cached track shape: video identifier, raw title, cleaned title, artist, duration.

Do not call `search.list` — the export tool has already done the matching. Quota is
not a consideration at this scale.

### Application key

A client-side key is unavoidable in a WebAssembly application. Restrict it by HTTP
referrer in the Google Cloud console and scope it to the YouTube Data API alone.
Read it from `wwwroot/appsettings.json`, gitignored, with a committed example file.

### Title cleaning

Treat this as a first-class problem. YouTube titles are inconsistent:
`Artist - Title (Official Video) [4K Remaster]`, `Title | Artist`,
`Artist — "Title" (Lyrics)`. Ingest must:

- Split artist and title on the common separators.
- Strip bracketed noise: official video, official audio, lyrics, high definition,
  remaster, visualiser, live at, extended, full album.
- Strip featured-artist clauses from the title for matching while keeping them for
  display.
- Retain both the raw and the cleaned title.

Provide a manual override in the playlist management screen. Parsing will get some
titles wrong, and a wrong title ruins both the autocomplete and the guess.

## Playback

The hard part. One-second precision is unforgiving.

### Timing is owned by C#

The YouTube IFrame Player is a JavaScript library controlling a cross-origin frame
over `postMessage`. There is no way to reach it from WebAssembly except through
interop, so a small amount of JavaScript is unavoidable. Keep it to a transport
shim with no logic of its own:

```
loadPlayer(elementIdentifier, dotNetReference)
cue(videoIdentifier)
seek(seconds)
play()
pause()
getCurrentTime() -> double
```

Nothing else belongs in that file. No branching, no state, no timing. It exists
solely because the frame boundary cannot be crossed from .NET directly.

All decisions live in C#: offset selection, ladder progression, and when a snippet
has run long enough. Resolve the interop reference as `IJSInProcessRuntime` so
calls are direct rather than marshalled through a task boundary.

The snippet loop is: cue, seek, play, then poll `getCurrentTime()` until the
elapsed media time reaches the target, then pause.

**Bound the snippet by media time, not wall-clock time.** A `Task.Delay` of the
snippet length would be accurate as a timer but would measure the wrong interval —
if the player stalls to rebuffer partway through, wall-clock keeps running while
audio does not, and a one-second snippet delivers noticeably less than a second of
sound. Only `getCurrentTime()` reports how much audio was actually heard. Poll it
at roughly frame rate and keep a generous wall-clock timeout as a failure backstop
in case playback never advances at all.

### Snippet selection

- Random track, excluding those played in the recent history window
  (approximately one fifth of the playlist length).
- A fresh random offset for **each rung** of the ladder — the two-second clip must
  come from a different part of the track than the one-second clip.
- Constrain the offset to between five and eighty-five percent of the duration,
  less the snippet length, to avoid silent introductions and fade-outs.

### Buffering

Cold-start buffering will otherwise consume most of a one-second clip.

- Cue the video, then seek to the target offset to force buffering at that position.
- Wait for the transition into the playing state before starting the timer.
- Show an explicit loading state. Never play into an unbuffered clip.
- Pre-cue the next round's video once the current round has been revealed.

### Advertisements

Non-subscribers may receive a pre-roll. Start the timer only when the player
reports the playing state *and* the reported current time is advancing near the
seek target. An advertisement must not consume the snippet.

### Autoplay policy

Browsers block unmuted playback without a user gesture. Every snippet is triggered
by a tap on the play control, which satisfies this. Never attempt playback outside
a gesture, including on the first round.

### Concealing the answer

The player must not reveal the video title, thumbnail, or channel. Cover it with an
opaque overlay positioned over the frame. Keep the frame rendered at a non-zero
size — hiding it with `display: none` stops playback.

> Fully obscuring the YouTube player sits in a grey area under the embed terms.
> Acceptable at roughly a hundred private users. Revisit before publicising or
> commercialising.

## Guessing

- Autocomplete over the cleaned titles of the current playlist.
- Correct means the title only. The artist is not required.
- Fuzzy matching on submission for freely typed input: normalise both sides by
  lowercasing and removing punctuation, diacritics, leading articles, and bracketed
  text, then accept on a Levenshtein distance of two or fewer. Tune against real
  playlist data.
- Skip advances the ladder and counts as a used guess.

## Statistics

Persisted per playlist in local storage:

- Rounds played and success rate
- Distribution across one, two, three, and failed
- Current and longest streak
- Per-track history, so that consistently missed songs are derivable later

## Screens

1. **Setup** — playlist address, ingest progress, discarded-track report.
2. **Game** — concealed player, ladder indicator, play control, autocomplete input,
   skip control.
3. **Reveal** — title, artist, thumbnail, link to the video, next control.
4. **Statistics** — distribution, streaks.
5. **Playlists** — switch, resynchronise, delete, manually correct parsed titles.

## Edge cases

- Video unavailable since ingest: skip the round silently, mark the track dead in
  the cache, re-roll.
- Fewer than roughly ten usable tracks: warn that the game is trivial.
- Region-blocked video: handle as unavailable.
- Player failure or network loss: retryable error that does not consume a guess.
- Track shorter than the snippet window: exclude at ingest.
- Resynchronising an existing playlist preserves statistics for surviving tracks.

## Build order

1. Ingest, caching, and title parsing, with a debug view of the parsed results.
2. The player shim and the C# snippet loop in isolation. Prove accurate one-second
   snippets under cold load, buffering, and pre-roll conditions before writing any
   game logic.
3. Ladder, guessing, fuzzy matching.
4. Statistics, playlist management, visual polish.

Step two is where this project succeeds or fails. Do not proceed until snippet
timing is reliably accurate on a cold load on a mobile browser.
