using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnglerHub.Mobile.Models;
using AnglerHub.Mobile.Services;

namespace AnglerHub.Mobile.ViewModels;

/// <summary>
/// Shows the yearly standings for the current club, with a cache-fallback pattern
/// that's reused across a few screens in the app: try the network first, and only
/// fall back to the last cached response for the exact same filter combination if
/// that fails — rather than just showing an error and an empty list.
/// </summary>
public partial class StandingsViewModel : ObservableObject
{
	private readonly ApiClient _api;
	private readonly ClubContextService _clubContext;
	private readonly OfflineDataCache _cache;

	public StandingsViewModel(ApiClient api, ClubContextService clubContext, OfflineDataCache cache)
	{
		_api = api;
		_clubContext = clubContext;
		_cache = cache;
		SelectedYear = AvailableYears.First();
	}

	public ObservableCollection<StandingsRowModel> Standings { get; } = new();

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	[ObservableProperty]
	public partial string? EmptyStateMessage { get; set; }

	/// <summary>When this list last actually came from the server — null until the first successful load.</summary>
	[ObservableProperty]
	public partial DateTime? LastSyncedAt { get; set; }

	/// <summary>True once the latest load attempt failed offline and what's shown is the previous cached snapshot.</summary>
	[ObservableProperty]
	public partial bool IsShowingCachedData { get; set; }

	public List<int> AvailableYears { get; } = Enumerable.Range(DateTime.Today.Year - 4, 5).Reverse().ToList();

	[ObservableProperty]
	public partial int SelectedYear { get; set; }

	partial void OnSelectedYearChanged(int value) => _ = LoadAsync();

	[RelayCommand]
	private async Task LoadAsync()
	{
		var clubId = _clubContext.CurrentClub?.Id;
		if (clubId is null) return;

		IsBusy = true;
		ErrorMessage = null;
		EmptyStateMessage = null;

		// The cache key includes every active filter, so each distinct filter
		// combination gets its own cache entry instead of overwriting one another.
		var cacheKey = $"standings_{clubId}_{SelectedYear}";

		try
		{
			var response = await _api.GetStandingsAsync(clubId.Value, SelectedYear);

			if (!response.Success || response.Data is null)
			{
				ErrorMessage = response.Message ?? "Could not load the standings.";
				await FallBackToCacheAsync(cacheKey);
				return;
			}

			ApplyData(response.Data);
			IsShowingCachedData = false;
			LastSyncedAt = DateTime.UtcNow;

			// Only cache a successful, fresh response — a failed/empty one should
			// never overwrite a previously good cache with nothing useful.
			await _cache.SetAsync(cacheKey, response.Data);
		}
		catch (Exception)
		{
			// No connection (or another network error) — this is exactly the
			// scenario the cache exists for.
			ErrorMessage = "Couldn't reach the server. Showing the last known standings if available.";
			await FallBackToCacheAsync(cacheKey);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task FallBackToCacheAsync(string cacheKey)
	{
		var cached = await _cache.GetAsync<StandingsResponse>(cacheKey);
		if (cached is null) return; // never cached for this exact filter combo yet — the error message stands

		ApplyData(cached);
		IsShowingCachedData = true;
		LastSyncedAt = await _cache.GetSyncedAtAsync(cacheKey);
	}

	private void ApplyData(StandingsResponse data)
	{
		Standings.Clear();
		foreach (var row in data.Standings) Standings.Add(row);

		if (Standings.Count == 0)
		{
			EmptyStateMessage = "No results yet.";
		}
	}
}

// Response DTO shape only — see Models/DomainModels.cs for the rest of the models.
public class StandingsResponse
{
	public List<StandingsRowModel> Standings { get; set; } = new();
}
