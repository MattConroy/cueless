using Cueless.Application;
using Cueless.Application.Playback;
using Cueless.Infrastructure;
using Cueless.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<IApplicationVersion, AssemblyApplicationVersion>();
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Playback").Get<PlaybackSettings>() ?? new PlaybackSettings());

await builder.Build().RunAsync();
