// ---------------------------------------------------------------------------
// Excerpt of MauiProgram.cs — the app registers ~25 pages/view models this
// way, following the same pattern shown below. Full list omitted.
// ---------------------------------------------------------------------------

using AnglerHub.Mobile.Services;
using AnglerHub.Mobile.ViewModels;
using AnglerHub.Mobile.Views;
using Plugin.Maui.OCR;

namespace AnglerHub.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();
		builder.UseMauiCommunityToolkit();
		builder.UseMauiCommunityToolkitCamera();
		builder.UseOcr();

		// ================= CONFIG =================
		// The one place that needs to change per environment (dev/staging/prod).
		builder.Services.AddSingleton<AppConfig>();

		// ================= HTTP / API =================
		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddHttpClient<ApiClient>((sp, client) =>
		{
			var config = sp.GetRequiredService<AppConfig>();
			client.BaseAddress = new Uri(config.ApiBaseUrl);
			client.Timeout = TimeSpan.FromSeconds(20);
		});

		// ================= OFFLINE STORAGE (weighings) =================
		builder.Services.AddSingleton<WeighingSyncQueue>();
		builder.Services.AddSingleton<OfflineDataCache>();
		builder.Services.AddSingleton<ConnectivityService>();

		// ================= SMART READING (photo -> weight) =================
		// Registered explicitly as IOcrService (rather than just adding
		// OcrPlugin.Default) so ScaleReadingService's constructor dependency
		// resolves cleanly instead of depending on the plugin's static type.
		builder.Services.AddSingleton<IOcrService>(OcrPlugin.Default);
		builder.Services.AddSingleton<ScaleReadingService>();

		// ================= APP STATE =================
		builder.Services.AddSingleton<ClubContextService>();

		// ================= PAGES + VIEW MODELS =================
		// Every screen follows the same Transient page + Transient view model pair.
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<LoginViewModel>();

		builder.Services.AddTransient<WeighingEntryPage>();
		builder.Services.AddTransient<WeighingEntryViewModel>();

		builder.Services.AddTransient<StandingsPage>();
		builder.Services.AddTransient<StandingsViewModel>();

		// ... ~20 more page/view-model pairs, omitted.

		return builder.Build();
	}
}
