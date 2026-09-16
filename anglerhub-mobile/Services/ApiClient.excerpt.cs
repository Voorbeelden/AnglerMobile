// ---------------------------------------------------------------------------
// This is a short excerpt of Services/ApiClient.cs, kept here to show the
// pattern used across the app. The real file wraps the full REST surface
// (~50 endpoints for auth, competitions, weighings, standings, clubs,
// payments...) and is not included in this repo — see the README for why.
// ---------------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AnglerHub.Mobile.Models;

namespace AnglerHub.Mobile.Services;

public class ApiClient
{
	private readonly HttpClient _http;
	private readonly SessionService _session;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	public ApiClient(HttpClient http, SessionService session)
	{
		_http = http;
		_session = session;
	}

	private void ApplyAuthHeader()
	{
		_http.DefaultRequestHeaders.Authorization = _session.Token is not null
			? new AuthenticationHeaderValue("Bearer", _session.Token)
			: null;

		// Makes sure server-side validation/error messages come back in whatever
		// language the app is currently set to.
		_http.DefaultRequestHeaders.AcceptLanguage.Clear();
		_http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(
			System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName));
	}

	// POST /auth/login
	public async Task<ApiResponse<LoginResult>> LoginAsync(string email, string password, string deviceName)
	{
		ApplyAuthHeader();

		var response = await _http.PostAsJsonAsync("auth/login", new
		{
			email,
			password,
			device_name = deviceName,
		});

		return await ReadAsync<LoginResult>(response);
	}

	// GET /auth/me
	public async Task<ApiResponse<MeResponse>> MeAsync()
	{
		ApplyAuthHeader();
		var response = await _http.GetAsync("auth/me");
		return await ReadAsync<MeResponse>(response);
	}

	// The other ~50 endpoint methods follow the same shape: apply the auth
	// header, call the endpoint, unwrap the response through ReadAsync<T>().
	// Omitted here.

	/// <summary>
	/// Every response from the API is a uniform { success, message, data } envelope
	/// (see ApiResponse&lt;T&gt;). This is the single place that deserializes it and
	/// turns a malformed/unexpected body into a normal failed ApiResponse instead of
	/// letting a JsonException bubble up into a page's UI code.
	/// </summary>
	private async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response)
	{
		var body = await response.Content.ReadAsStringAsync();

		try
		{
			var parsed = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
			if (parsed is not null) return parsed;
		}
		catch (JsonException)
		{
			return new ApiResponse<T> { Success = false, Message = "Unexpected response from the server." };
		}

		return new ApiResponse<T>
		{
			Success = false,
			Message = response.IsSuccessStatusCode
				? "Unexpected response from the server."
				: $"Server returned {(int)response.StatusCode}.",
		};
	}
}

public class LoginResult
{
	public bool RequiresTwoFactor { get; set; }
	public string? ChallengeToken { get; set; }
	public string? Token { get; set; }
	public UserModel? User { get; set; }
}
