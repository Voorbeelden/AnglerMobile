using System.Text.Json;
using AnglerHub.Mobile.Models;

namespace AnglerHub.Mobile.Services;

/// <summary>
/// Holds the API token and the current user via SecureStorage (iOS Keychain / Android
/// Keystore / Windows Credential Locker — encrypted at rest), never plain Preferences.
/// The user object contains personal data (name, email, role), so it gets the same
/// treatment as the token itself rather than being cached in regular settings storage.
/// </summary>
public class SessionService
{
	private const string TokenKey = "auth_token";
	private const string UserKey = "auth_user";

	public string? Token { get; private set; }
	public UserModel? CurrentUser { get; private set; }
	public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

	public async Task RestoreAsync()
	{
		Token = await SecureStorage.Default.GetAsync(TokenKey);

		var userJson = await SecureStorage.Default.GetAsync(UserKey);
		if (!string.IsNullOrEmpty(userJson))
		{
			CurrentUser = JsonSerializer.Deserialize<UserModel>(userJson);
		}
	}

	public async Task SetSessionAsync(string token, UserModel user)
	{
		Token = token;
		CurrentUser = user;

		await SecureStorage.Default.SetAsync(TokenKey, token);
		await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(user));
	}

	public void ClearSession()
	{
		Token = null;
		CurrentUser = null;

		SecureStorage.Default.Remove(TokenKey);
		SecureStorage.Default.Remove(UserKey);
	}
}
