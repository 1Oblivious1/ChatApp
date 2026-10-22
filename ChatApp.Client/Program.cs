using ChatApp.Client.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// По умолчанию — http-профиль из ChatApp.Api/Properties/launchSettings.json
const string defaultBaseUrl = "http://localhost:5273";

Console.Write($"Адрес сервера [{defaultBaseUrl}]: ");
var baseUrlInput = Console.ReadLine();
var baseUrl = string.IsNullOrWhiteSpace(baseUrlInput) ? defaultBaseUrl : baseUrlInput;

var api = new ApiClient(baseUrl);

await RunAuthMenuAsync();

if (!api.IsAuthenticated)
{
    Console.WriteLine("Не удалось авторизоваться. Выход.");
    return;
}

// HubClient создаётся один раз после логина. На каждом (пере)подключении хаб
// запрашивает токен заново, и если тот уже протух, ApiClient сначала обновит его через refresh.
await using var hub = new HubClient(baseUrl, api.GetValidAccessTokenAsync);

hub.MessageReceived += msg =>
{
    // Console.WriteLine из обработчика события SignalR вызывается в фоновом потоке —
    // возможно наложение с тем, что пользователь как раз печатает в этот момент.
    // Для учебного консольного клиента это приемлемое упрощение; в реальном
    // приложении тут был бы полноценный UI-фреймворк с раздельными областями вывода.
    Console.WriteLine($"\n[{msg.SentAt:HH:mm:ss}] {msg.SenderId}: {msg.Content}");
    Console.Write("> ");
};
hub.UserOnline += userId => Console.WriteLine($"\n(пользователь {userId} в сети)");
hub.UserOffline += userId => Console.WriteLine($"\n(пользователь {userId} вышел из сети)");
hub.UserTyping += (userId, roomId) => Console.WriteLine($"\n(пользователь {userId} печатает в комнате {roomId})");

Console.WriteLine("Подключаюсь к чату...");
await hub.StartAsync();
Console.WriteLine($"Подключено как {api.CurrentUserName} ({api.CurrentUserId})");

await RunMainMenuAsync();

// ---------- Локальные функции ----------

async Task RunAuthMenuAsync()
{
    while (!api.IsAuthenticated)
    {
        Console.WriteLine("\n1) Войти  2) Зарегистрироваться  3) Выход");
        Console.Write("> ");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                Console.Write("Логин или email: ");
                var loginName = Console.ReadLine() ?? "";
                Console.Write("Пароль: ");
                var loginPassword = ReadPassword();

                var (loginOk, loginError) = await api.LoginAsync(loginName, loginPassword);
                if (!loginOk)
                    Console.WriteLine($"Ошибка входа: {loginError}");
                break;

            case "2":
                Console.Write("Имя пользователя: ");
                var userName = Console.ReadLine() ?? "";
                Console.Write("Email: ");
                var email = Console.ReadLine() ?? "";
                Console.Write("Пароль (мин. 8 символов): ");
                var password = ReadPassword();

                var (regOk, regError) = await api.RegisterAsync(userName, email, password);
                if (!regOk)
                    Console.WriteLine($"Ошибка регистрации: {regError}");
                break;

            case "3":
                return;

            default:
                Console.WriteLine("Не понял выбор, попробуй ещё раз.");
                break;
        }
    }
}

async Task RunMainMenuAsync()
{
    while (true)
    {
        Console.WriteLine("\n--- Главное меню ---");
        Console.WriteLine("1) Мои комнаты");
        Console.WriteLine("2) Создать комнату");
        Console.WriteLine("3) Открыть комнату (чат)");
        Console.WriteLine("4) Выход");
        Console.Write("> ");

        switch (Console.ReadLine())
        {
            case "1":
                await ListRoomsAsync();
                break;
            case "2":
                await CreateRoomAsync();
                break;
            case "3":
                await OpenRoomAsync();
                break;
            case "4":
                return;
            default:
                Console.WriteLine("Не понял выбор.");
                break;
        }
    }
}

async Task ListRoomsAsync()
{
    var (rooms, error) = await api.GetMyRoomsAsync();
    if (rooms is null)
    {
        Console.WriteLine($"Ошибка: {error}");
        return;
    }

    if (rooms.Count == 0)
    {
        Console.WriteLine("У тебя пока нет комнат.");
        return;
    }

    foreach (var room in rooms)
    {
        var title = room.Name ?? (room.IsDirect ? "Личный диалог" : "Без названия");
        var members = string.Join(", ", room.Members.Select(m => m.UserName));
        Console.WriteLine($"[{room.Id}] {title} — участники: {members}");
    }
}

async Task CreateRoomAsync()
{
    Console.Write("Название комнаты (пусто — для личного диалога): ");
    var name = Console.ReadLine();
    name = string.IsNullOrWhiteSpace(name) ? null : name;

    Console.Write("Это личный диалог 1-на-1? (y/n): ");
    var isDirect = (Console.ReadLine() ?? "").Trim().ToLower() == "y";

    Console.Write("UserId участников через запятую (без себя — тебя добавят автоматически): ");
    var idsInput = Console.ReadLine() ?? "";
    var ids = idsInput
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
        .Where(g => g.HasValue)
        .Select(g => g!.Value)
        .ToList();

    var (room, error) = await api.CreateRoomAsync(name, isDirect, ids);
    if (room is null)
    {
        Console.WriteLine($"Ошибка: {error}");
        return;
    }

    Console.WriteLine($"Комната создана: {room.Id}");
}

async Task OpenRoomAsync()
{
    Console.Write("Id комнаты: ");
    if (!Guid.TryParse(Console.ReadLine(), out var roomId))
    {
        Console.WriteLine("Некорректный Id.");
        return;
    }

    await hub.JoinRoomAsync(roomId);

    var (history, error) = await api.GetHistoryAsync(roomId);
    if (history is null)
    {
        Console.WriteLine($"Не удалось загрузить историю: {error}");
        return;
    }

    Console.WriteLine("--- История (от старых к новым) ---");
    foreach (var msg in Enumerable.Reverse(history)) // сервер отдаёт DESC, для чтения разворачиваем
        Console.WriteLine($"[{msg.SentAt:HH:mm:ss}] {msg.SenderUserName}: {msg.Content}");

    Console.WriteLine("--- Чат открыт. Пиши сообщения, 'exit' — выйти в меню ---");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();

        if (input is null || input.Trim().ToLower() == "exit")
            break;

        if (string.IsNullOrWhiteSpace(input))
            continue;

        try
        {
            await hub.SendMessageAsync(roomId, input);
        }
        catch (Exception ex)
        {
            // HubException от сервера (например, "не участник комнаты") прилетает сюда
            Console.WriteLine($"Не удалось отправить: {ex.Message}");
        }
    }
}

static string ReadPassword()
{
    var password = new System.Text.StringBuilder();
    ConsoleKeyInfo key;

    do
    {
        key = Console.ReadKey(intercept: true);

        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write("*");
        }
    } while (key.Key != ConsoleKey.Enter);

    Console.WriteLine();
    return password.ToString();
}