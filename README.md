# Cueless

A solo music-guessing game over a YouTube playlist. A one-second snippet from a
random point in a random track; guess the title. Miss it and the snippet grows to
two seconds, then three, then the answer. The score is the number of guesses it
took.

Blazor WebAssembly, statically hosted at <https://mattconroy.github.io/cueless/>.

## Layout

| Project | Holds |
| --- | --- |
| `Cueless.Domain` | Entities, value objects, game rules. Depends on nothing. |
| `Cueless.Application` | Use cases, abstractions for playback and persistence. |
| `Cueless.Infrastructure` | YouTube Data API client, local storage adapter. |
| `Cueless.Web` | Components and JavaScript interop. |

## Running it

Requires the .NET SDK version in `global.json` and the WebAssembly tools workload.

```
dotnet workload install wasm-tools
cp src/Cueless.Web/wwwroot/appsettings.example.json src/Cueless.Web/wwwroot/appsettings.json
dotnet run --project src/Cueless.Web
```

`appsettings.json` holds a YouTube Data API key and is not committed. Create a key
in the Google Cloud console, scope it to the YouTube Data API alone, and restrict it
by HTTP referrer.

```
dotnet build
dotnet test
```

## Deployment

Pull requests and pushes to `main` build, test and publish. Neither deploys.

Deployment to GitHub Pages happens two ways:

- Pushing a version tag such as `1.2.3` deploys and creates a release with the
  published site attached.
- Running the workflow manually against any branch deploys that branch, for trying a
  change on a real device before merging it.

There is one Pages site, so a deployment replaces whatever is live until the next one.
Each deployment records the ref it published in the workflow run summary.

The tag becomes the assembly version and is shown in the page footer. Builds that do
not come from a tag are versioned `0.0.0` with the short commit identifier appended,
so a manually deployed branch is identifiable.

Publishing rewrites the base address to the repository subpath and copies `index.html`
to `404.html` so that deep links resolve client side. The deployed site reads its key
from the `YOUTUBE_DATA_API_KEY` repository secret.
