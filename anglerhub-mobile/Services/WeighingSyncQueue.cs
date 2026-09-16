using AnglerHub.Mobile.Models;
using SQLite;

namespace AnglerHub.Mobile.Services;

/// <summary>
/// Core offline-first flow of the app: anglers record catch weights at the waterside,
/// often with no signal at all. Every weighing is written to a local SQLite table first;
/// only after that do we try to push it to the server. If that fails it just sits here
/// as "Pending" and gets retried automatically once ConnectivityService reports we're
/// back online.
///
/// The ClientUuid on each entry (see PendingWeighing) is what makes the server-side
/// endpoint idempotent — if a weighing accidentally gets synced twice, the API only
/// processes it once.
/// </summary>
public class WeighingSyncQueue
{
	private readonly SQLiteAsyncConnection _db;
	private readonly ApiClient _api;
	private readonly ConnectivityService _connectivity;
	private readonly SessionService _session;
	private readonly SemaphoreSlim _syncLock = new(1, 1);

	// Table creation is awaited lazily at the top of every public method instead of
	// blocked on in the constructor — this used to call .Wait() directly on the
	// async init, which stalls the thread creating the singleton (usually app
	// startup) and can deadlock depending on the sync context.
	private readonly Task _initTask;

	public event EventHandler? QueueChanged;

	public WeighingSyncQueue(ApiClient api, ConnectivityService connectivity, SessionService session)
	{
		_api = api;
		_connectivity = connectivity;
		_session = session;

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "weighing_queue.db3");
		_db = new SQLiteAsyncConnection(dbPath);
		_initTask = _db.CreateTableAsync<PendingWeighing>();

		_connectivity.ConnectivityRestored += async (_, _) => await SyncPendingAsync();
	}

	/// <summary>
	/// Always writes locally first, then tries to send immediately if we're online.
	/// Never throws to the UI — recording a catch should never get blocked by a
	/// network hiccup.
	/// </summary>
	public async Task<PendingWeighing> RecordWeighingAsync(
		int competitionId, int userId, string userName, decimal weight,
		string? photoPath = null, int? cropX = null, int? cropY = null, int? cropWidth = null, int? cropHeight = null)
	{
		await _initTask;

		var pending = new PendingWeighing
		{
			CompetitionId = competitionId,
			UserId = userId,
			UserName = userName,
			Weight = weight,
			Status = SyncStatus.Pending,
			PhotoPath = photoPath,
			CropX = cropX,
			CropY = cropY,
			CropWidth = cropWidth,
			CropHeight = cropHeight,
			WasSmartRead = photoPath is not null,
			RecordedByUserId = _session.CurrentUser?.Id ?? 0,
		};

		await _db.InsertAsync(pending);
		QueueChanged?.Invoke(this, EventArgs.Empty);

		if (_connectivity.IsOnline)
		{
			await SyncPendingAsync();
		}

		return pending;
	}

	public async Task<int> GetPendingCountAsync()
	{
		await _initTask;
		var myId = _session.CurrentUser?.Id ?? 0;

		return await _db.Table<PendingWeighing>()
			.Where(w => w.Status == SyncStatus.Pending && w.RecordedByUserId == myId)
			.CountAsync();
	}

	/// <summary>
	/// Used to block logout while there are still unsynced weighings on this device —
	/// nobody should accidentally lose an evening's catches by signing out too early.
	/// </summary>
	public async Task<bool> HasUnsyncedEntriesAsync() => await GetPendingCountAsync() > 0;

	/// <summary>
	/// Sends every not-yet-synced weighing, grouped per competition (the API expects
	/// one batch per competition). Runs automatically on reconnect / after each new
	/// weighing, and can also be triggered manually from the UI.
	/// </summary>
	public async Task SyncPendingAsync()
	{
		await _initTask;

		if (!await _syncLock.WaitAsync(0))
		{
			return; // a sync is already running, this call just piggybacks on it
		}

		try
		{
			if (!_connectivity.IsOnline) return;

			var myId = _session.CurrentUser?.Id ?? 0;
			var pending = await _db.Table<PendingWeighing>()
				.Where(w => w.Status == SyncStatus.Pending && w.RecordedByUserId == myId)
				.ToListAsync();

			if (pending.Count == 0) return;

			foreach (var group in pending.GroupBy(w => w.CompetitionId))
			{
				await SyncGroupAsync(group.ToList());
			}

			QueueChanged?.Invoke(this, EventArgs.Empty);
		}
		finally
		{
			_syncLock.Release();
		}
	}

	private async Task SyncGroupAsync(List<PendingWeighing> items)
	{
		// Entries with a photo (smart-reading) go one by one over multipart — a file
		// doesn't belong in a JSON batch. Everything else (the vast majority — plain
		// manual weight entry) goes together in a single batch call.
		var withPhoto = items.Where(i => i.PhotoPath is not null).ToList();
		var withoutPhoto = items.Where(i => i.PhotoPath is null).ToList();

		foreach (var item in withPhoto)
		{
			await SyncSingleWithPhotoAsync(item);
		}

		if (withoutPhoto.Count > 0)
		{
			await SyncBatchAsync(withoutPhoto);
		}
	}

	// --- the two SyncXAsync() implementations, the "capped / disqualified" result
	// mapping and the local photo cleanup are omitted from this showcase copy.
	// They follow the same try/catch-and-stay-pending shape as SyncPendingAsync()
	// above: a failed batch or a failed single upload just leaves the affected rows
	// as Pending and lets the next reconnect retry them, nothing throws to the UI. ---
}
