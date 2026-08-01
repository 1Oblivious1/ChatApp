using ChatApp.Client.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ChatApp.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private string? _refreshToken;

    public Guid? CurrentUserId { get; private set; }
    public string? CurrentUserName { get; private set; }
    public bool IsAuthenticated => _accessToken is not null;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public string? AccessToken => _accessToken;

    private void ApplyAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _accessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private void SetTokens(AuthResponse auth)
    {
        _accessToken = auth.AccessToken;
        _refreshToken = auth.RefreshToken;
        CurrentUserId = auth.UserId;
        CurrentUserName = auth.UserName;
        ApplyAuthHeader();
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string userName, string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", new
        {
            userName,
            email,
            password
        });

        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response));

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        SetTokens(auth!);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string userNameOrEmail, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", new { userNameOrEmail, password });

        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response));

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        SetTokens(auth!);
        return (true, null);
    }

    private async Task<bool> TryRefreshAsync()
    {
        if (_refreshToken is null) return false;

        var response = await _http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });
        if (!response.IsSuccessStatusCode)
            return false;

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        SetTokens(auth!);
        return true;
    }

    /// <summary>
    /// Выполняет запрос; если сервер ответил 401 (access-токен протух за время
    /// работы клиента), один раз пробует обновиться через refresh-токен и повторяет
    /// запрос. Это тот же механизм, что описывался при проектировании TokenService —
    /// здесь просто его потребитель на стороне клиента.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> send)
    {
        var response = await send();

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync())
        {
            response.Dispose();
            response = await send();
        }

        return response;
    }

    public async Task<(List<RoomResponse>? Rooms, string? Error)> GetMyRoomsAsync()
    {
        var response = await SendWithRefreshAsync(() => _http.GetAsync("/api/rooms"));

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        return (rooms, null);
    }

    public async Task<(RoomResponse? Room, string? Error)> CreateRoomAsync(string? name, bool isDirect, List<Guid> memberIds)
    {
        var response = await SendWithRefreshAsync(() =>
            _http.PostAsJsonAsync("/api/rooms", new { name, isDirect, memberUserIds = memberIds }));

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        return (room, null);
    }

    public async Task<(List<MessageResponse>? Messages, string? Error)> GetHistoryAsync(Guid roomId, long? beforeId = null, int take = 50)
    {
        var query = beforeId is null
            ? $"/api/rooms/{roomId}/messages?take={take}"
            : $"/api/rooms/{roomId}/messages?take={take}&beforeId={beforeId}";

        var response = await SendWithRefreshAsync(() => _http.GetAsync(query));

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
        return (messages, null);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body)
            ? $"HTTP {(int)response.StatusCode}"
            : $"HTTP {(int)response.StatusCode}: {body}";
    }
}