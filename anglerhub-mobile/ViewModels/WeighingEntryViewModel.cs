using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnglerHub.Mobile.Models;
using AnglerHub.Mobile.Services;

namespace AnglerHub.Mobile.ViewModels;

/// <summary>
/// The screen anglers use to record weights during a competition. This is the view
/// model where the offline-first pieces (WeighingSyncQueue) and the optional
/// camera/OCR shortcut (ScaleReadingService) come together into a single form.
/// Trimmed here to the save flow and the running totals — the participant loading,
/// pair-competition deduplication and camera capture handling are left out.
/// </summary>
[QueryProperty(nameof(CompetitionIdText), "id")]
public partial class WeighingEntryViewModel : ObservableObject
{
	private readonly ApiClient _api;
	private readonly WeighingSyncQueue _queue;
	private int _competitionId;

	public WeighingEntryViewModel(ApiClient api, WeighingSyncQueue queue)
	{
		_api = api;
		_queue = queue;

		// Fires after every successful background sync too (see
		// WeighingSyncQueue.SyncPendingAsync), which is the moment a just-synced
		// entry needs to move from "pending" into "already recorded (server)"
		// without the user having to leave and re-enter the screen.
		_queue.QueueChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshPendingAsync);
	}

	[ObservableProperty]
	public partial string CompetitionIdText { get; set; } = string.Empty;

	public ObservableCollection<ParticipantModel> Participants { get; } = new();

	[ObservableProperty]
	public partial int WeighedCount { get; set; }

	[ObservableProperty]
	public partial int TotalParticipantsCount { get; set; }

	[ObservableProperty]
	public partial decimal TotalWeight { get; set; }

	private void RecalculateTotals()
	{
		TotalParticipantsCount = Participants.Count;
		WeighedCount = Participants.Count(p => p.CurrentWeight > 0);
		TotalWeight = Participants.Sum(p => p.CurrentWeight);
	}

	[ObservableProperty]
	public partial ParticipantModel? SelectedParticipant { get; set; }

	[ObservableProperty]
	public partial string WeightInput { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsSaving { get; set; }

	[ObservableProperty]
	public partial string? FeedbackMessage { get; set; }

	[RelayCommand]
	private async Task RecordWeighingAsync()
	{
		if (IsSaving) return; // simple double-tap guard, there's no native form submit behaviour to rely on here

		if (SelectedParticipant is null)
		{
			FeedbackMessage = "Select a participant first.";
			return;
		}

		if (!decimal.TryParse(WeightInput.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var weight) || weight <= 0)
		{
			FeedbackMessage = "Enter a valid weight.";
			return;
		}

		IsSaving = true;
		FeedbackMessage = null;
		var participant = SelectedParticipant;

		try
		{
			// This is the heart of the offline-first behaviour: always save locally
			// first (works guaranteed, even with no signal), then sync if possible.
			// We never block the UI on a server round-trip here.
			await _queue.RecordWeighingAsync(_competitionId, participant.User.Id, participant.User.Name, weight);

			// Optimistically update the running total right away instead of waiting
			// for a server round-trip — the server remains the source of truth once
			// a sync actually happens.
			participant.CurrentWeight += weight;
			RecalculateTotals();

			WeightInput = string.Empty;
			SelectedParticipant = null;
			FeedbackMessage = $"Weight recorded for {participant.DisplayName}.";
		}
		finally
		{
			IsSaving = false;
		}
	}

	private async Task RefreshPendingAsync()
	{
		// Reloads the "pending" and "synced" lists for this competition — omitted here.
		await Task.CompletedTask;
	}
}
