using System.Text.Json.Serialization;

namespace AnglerHub.Mobile.Models;

// A representative slice of the ~25 DTOs in the app. Every one of them mirrors a
// specific API resource on the backend 1:1 (field for field), which is what makes
// keeping ApiClient.cs, this file and the server's response resources in sync the
// main "moving part" whenever the API contract changes.

public class UserModel
{
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("email")] public string? Email { get; set; }
	[JsonPropertyName("role")] public string? Role { get; set; }
}

public class CompetitionModel
{
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("club_id")] public int ClubId { get; set; }
	[JsonPropertyName("venue")] public VenueModel? Venue { get; set; }
	[JsonPropertyName("date")] public string? Date { get; set; }
	[JsonPropertyName("fee")] public decimal Fee { get; set; }
	[JsonPropertyName("max_participants")] public int? MaxParticipants { get; set; }
	[JsonPropertyName("is_ranked")] public bool IsRanked { get; set; }
	[JsonPropertyName("uses_sectors")] public bool UsesSectors { get; set; }
	[JsonPropertyName("is_started")] public bool IsStarted { get; set; }
	[JsonPropertyName("is_closed")] public bool IsClosed { get; set; }

	// Whether the competition is actually allowed to start (at least one participant
	// has a draw / assigned peg). Kept as a plain bool from the server rather than
	// re-deriving the same rule client-side, so the mobile app and the web app can
	// never quietly disagree on when the start button should be enabled.
	[JsonPropertyName("can_start")] public bool CanStart { get; set; }

	[JsonPropertyName("total_weight")] public decimal TotalWeight { get; set; }
	[JsonPropertyName("weighed_count")] public int WeighedCount { get; set; }
	[JsonPropertyName("total_participants_count")] public int TotalParticipantsCount { get; set; }

	public string StatusLabel => (IsStarted, IsClosed) switch
	{
		(true, true) => "Closed",
		(true, false) => "In progress",
		(false, _) => "Not started",
	};

	public string StatusColorKey => (IsStarted, IsClosed) switch
	{
		(true, true) => "TextMuted",
		(true, false) => "Primary",
		(false, _) => "TextFaint",
	};

	public DateTime? DateParsed => DateTime.TryParse(Date, out var d) ? d : null;
}

public class ParticipantModel
{
	[JsonPropertyName("user")] public UserModel User { get; set; } = new();
	[JsonPropertyName("peg_number")] public int? PegNumber { get; set; }
	[JsonPropertyName("result_id")] public int ResultId { get; set; }
	[JsonPropertyName("current_weight")] public decimal CurrentWeight { get; set; }
	[JsonPropertyName("sector")] public string? Sector { get; set; }

	// Filled in client-side after loading (see the standings/weighing view models) for
	// pair competitions: the other participant(s) sharing the same ResultId. Null
	// outside a pair format.
	public List<UserModel>? Partners { get; set; }

	public string DisplayName => Partners is { Count: > 0 }
		? $"{User.Name} + {string.Join(", ", Partners.Select(p => p.Name))}"
		: User.Name;
}

public class WeighingModel
{
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("result_id")] public int ResultId { get; set; }
	[JsonPropertyName("participant")] public UserModel? Participant { get; set; }
	[JsonPropertyName("weight")] public decimal Weight { get; set; }
	[JsonPropertyName("original_weight")] public decimal OriginalWeight { get; set; }
	[JsonPropertyName("capped")] public bool Capped { get; set; }
	[JsonPropertyName("disqualified")] public bool Disqualified { get; set; }
	[JsonPropertyName("client_uuid")] public string? ClientUuid { get; set; }
	[JsonPropertyName("photo_url")] public string? PhotoUrl { get; set; }
	[JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
}

public class StandingsRowModel
{
	[JsonPropertyName("rank")] public int Rank { get; set; }
	[JsonPropertyName("user")] public UserModel User { get; set; } = new();
	[JsonPropertyName("partners")] public List<UserModel>? Partners { get; set; }
	[JsonPropertyName("total_weight")] public decimal TotalWeight { get; set; }
	[JsonPropertyName("total_points")] public int? TotalPoints { get; set; }
	[JsonPropertyName("competitions_count")] public int CompetitionsCount { get; set; }

	public string DisplayName => Partners is { Count: > 0 }
		? $"{User.Name} + {string.Join(", ", Partners.Select(p => p.Name))}"
		: User.Name;
}

public class VenueModel
{
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("address")] public string? Address { get; set; }
}

// Roughly 20 more models (club, subscription/billing, payments, registration,
// renewals, sectors...) follow the same flat DTO shape and are left out here.
