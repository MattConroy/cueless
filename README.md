# Cueless

A solo music-guessing game over your own YouTube playlists. A one-second snippet
from a random point in a random track; guess the title. Miss it and the snippet
grows to two seconds, then three, then the answer. Your score is the number of
guesses it took.

Blazor WebAssembly, statically hosted on GitHub Pages. No accounts, no server,
no Spotify. Playlists are converted to YouTube by the player using a tool such as
Soundiiz or TuneMyMusic.

Live at <https://mattconroy.github.io/cueless/>.

## Layout

| Project | Holds |
| --- | --- |
| `Cueless.Domain` | Entities, value objects, game rules. No dependencies. |
| `Cueless.Application` | Use cases, abstractions for playback and persistence. |
| `Cueless.Infrastructure` | YouTube Data API client, local storage adapter. |
| `Cueless.Web` | Components and JavaScript interop. |

`Cueless.Domain.Tests` and `Cueless.Application.Tests` cover the inner two layers
and guard the dependency direction between them.

## Running it

Requires the .NET SDK pinned in `global.json` and the WebAssembly tools workload.

```
dotnet workload install wasm-tools
cp src/Cueless.Web/wwwroot/appsettings.example.json src/Cueless.Web/wwwroot/appsettings.json
dotnet run --project src/Cueless.Web
```

`appsettings.json` holds a YouTube Data API key and is not committed. Create one
in the Google Cloud console, scope it to the YouTube Data API alone, and restrict
it by HTTP referrer. The deployed site takes its key from the
`YOUTUBE_DATA_API_KEY` repository secret.

```
dotnet build
dotnet test
```

## Deployment

`.github/workflows/ci.yml` builds and tests every pull request, publishes the
static site, and deploys it to GitHub Pages on every push to `main`. Publishing
rewrites the base address to the repository subpath and copies `index.html` to
`404.html` so that deep links resolve client side.

The build spec is in [docs/specification.md](docs/specification.md).
