using ChatApp.Client.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ChatApp.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;
    private string? _refreshToken;

    // Refresh теперь может запуститься одновременно из REST-запроса и из реконнекта хаба.
    // Без блокировки оба отправили бы ОДИН и тот же refresh-токен, и сервер принял бы
    // второй запрос за кражу и отозвал бы все сессии.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public Guid? CurrentUserId { get; private set; }
    public string? CurrentUserName { get; private set; }
    public bool IsAuthenticated => _accessToken is not null;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    /// <summary>
    /// Access-токен для подключения к хабу. Сообщения ходят через SignalR, а не через REST,
    /// поэтому SendWithRefreshAsync в долгом чате ни разу не вызывается и токен успевает
    /// протухнуть — тогда при реконнекте хаб получил бы 401. Здесь обновляем его заранее.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync()
    {
        // Небольшой запас на рассинхрон часов клиента и сервера (на сервере ClockSkew = 0)
        if (DateTime.UtcNow < _accessTokenExpiresAt - TimeSpan.FromSeconds(30))
            return _accessToken;

        await TryRefreshAsync();
        return _accessToken;
    }

    private void ApplyAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _accessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private void SetTokens(AuthResponse auth)
    {
        _accessToken = auth.AccessToken;
        _accessTokenExpiresAt = auth.AccessTokenExpiresAt;
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
        await _refreshLock.WaitAsync();
        try
        {
            if (_refreshToken is null) return false;

            var response = await _http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });
            if (!response.IsSuccessStatusCode)
                return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            SetTokens(auth!);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
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