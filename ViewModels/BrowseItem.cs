
using QuiverLauncher.Models;
using QuiverLauncher.Services;

namespace QuiverLauncher.ViewModels;

/// <summary>Карточка приложения в каталоге Quiver Launcher или приложения из собственной библиотеки пользователя.</summary>
public sealed class BrowseItem : ObservableViewModel, IBrowseCard
{
    private bool _inLibrary;
    private bool _isGamepadFocused;
    private bool _isHovered;

    private BrowseItem(QuiverCatalogApp? app, GameInfo? listApp, string title, string subtitle, string? imageUrl, string kind,
        string cardKind, IReadOnlyList<string> basedOn)
    {
        App = app;
        ListApp = listApp;
        Title = title;
        Subtitle = subtitle;
        ImageUrl = imageUrl;
        Kind = kind;
        CardKind = cardKind.ToUpperInvariant();
        BasedOn = basedOn;
        var os = app?.SupportedOS ?? [];
        RunsOnWindows = os.Contains("windows");
        RunsOnMacOS = os.Contains("macos");
        RunsOnLinux = os.Contains("linux");
        RunsOnAndroid = os.Contains("android");
        RunsOnIOS = os.Contains("ios");

        // Платформы перечисляются в том же порядке, что и на сайте.
        PlatformTip = BrowseText.PlatformNames(new[] { "windows", "macos", "linux", "android", "ios" }.Where(os.Contains));
    }

    /// <summary>Приложение из каталога или null, если приложение взято из собственной библиотеки.</summary>
    public QuiverCatalogApp? App { get; }

    /// <summary>Приложение из собственной библиотеки в формате библиотеки.</summary>
    public GameInfo? ListApp { get; }

    public string Title { get; }
    public string Subtitle { get; }
    public string? ImageUrl { get; }
    public string Kind { get; }
    public bool HasSubtitle => Subtitle.Length > 0;
    public bool HasKind => Kind.Length > 0;

    // Карточка показывает теги и платформы, название, игры-основы, состояние запуска и дату обновления.
    /// <summary>Первые два тега приложения или названия консолей в верхнем регистре.</summary>
    public string CardKind { get; }

    public IReadOnlyList<string> BasedOn { get; }
    public bool HasBasedOn => BasedOn.Count > 0;
    public bool RunsOnWindows { get; }
    public bool RunsOnMacOS { get; }
    public bool RunsOnLinux { get; }
    public bool RunsOnAndroid { get; }
    public bool RunsOnIOS { get; }
    public string PlatformTip { get; }

    public string AiChip => App?.AiLevel switch
    {
        "assisted" => "С ПОМОЩЬЮ ИИ",
        "generated" => "СОЗДАНО ИИ",
        _ => ""
    };

    public bool HasAiChip => AiChip.Length > 0;
    public bool HasFooter => App != null;

    public string ScoreLabel => App == null ? "" : BrowseText.ScoreParts(App.Recommended, App.ReportIssues, App.ReportBroken).Label;
    public string ScoreCounts => App == null ? "" : BrowseText.ScoreParts(App.Recommended, App.ReportIssues, App.ReportBroken).Counts;
    public string ScoreTone => App == null ? "" : BrowseText.ScoreParts(App.Recommended, App.ReportIssues, App.ReportBroken).Tone;

    public bool ScorePositive => ScoreTone == "positive";
    public bool ScoreCaution => ScoreTone == "caution";
    public bool ScoreNegative => ScoreTone == "negative";
    public bool ScoreMuted => ScoreTone == "muted";

    public bool ReleaseStale => App != null &&
        (ReleaseText.StartsWith("Релизов нет", StringComparison.Ordinal) ||
         ReleaseText.StartsWith("Без релизов", StringComparison.Ordinal));

    public string FolderName => App != null ? QuiverCatalogMapping.FolderFor(App) : ListApp?.FolderName ?? "";

    public string ScoreText => App == null ? "" : BrowseText.Score(App.Recommended, App.ReportIssues, App.ReportBroken);
    public string ReleaseText => App == null ? "" : BrowseText.ReleaseAge(App.LastReleaseAt, DateTimeOffset.UtcNow);

    public bool IsNew => App != null &&
        DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds((long)App.AddedAt) < TimeSpan.FromDays(30);

    public string Badge => InLibrary ? "В библиотеке" : IsNew ? "Новинка" : "";
    public bool HasBadge => Badge.Length > 0;
    public string BadgeText => Badge.ToUpperInvariant();

    public bool InLibrary
    {
        get => _inLibrary;
        set
        {
            if (Set(ref _inLibrary, value))
            {
                Notify(nameof(Badge));
                Notify(nameof(BadgeText));
                Notify(nameof(HasBadge));
                Notify(nameof(CanAdd));
                Notify(nameof(ShowCardAction));
                Notify(nameof(CardActionText));
                Notify(nameof(CardActionTip));
            }
        }
    }

    private bool _isAdding;

    /// <summary>В данный момент приложение добавляется в библиотеку.</summary>
    public bool IsAdding
    {
        get => _isAdding;
        set
        {
            if (Set(ref _isAdding, value))
            {
                Notify(nameof(AddText));
                Notify(nameof(CardActionText));
            }
        }
    }

    /// <summary>Можно добавить приложение из каталога в библиотеку, не открывая его страницу.</summary>
    public bool CanAdd => !InLibrary && (App != null || ListApp != null);

    public string AddText => IsAdding ? "Добавление…" : "+ Добавить";

    /// <summary>Кнопка карточки: добавление или открытие приложения в библиотеке.</summary>
    public bool ShowCardAction => CanAdd || (InLibrary && (App != null || ListApp != null));

    public string CardActionText => InLibrary && !IsAdding ? "Открыть в библиотеке" : AddText;
    public string CardActionTip => InLibrary ? "Показать в библиотеке" : "Добавить в библиотеку";

    public bool IsGamepadFocused
    {
        get => _isGamepadFocused;
        set
        {
            if (Set(ref _isGamepadFocused, value))
                Notify(nameof(ShouldScroll));
        }
    }

    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (Set(ref _isHovered, value))
                Notify(nameof(ShouldScroll));
        }
    }

    /// <summary>Обрезанный текст прокручивается, когда курсор или фокус геймпада находятся на карточке.</summary>
    public bool ShouldScroll => IsHovered || IsGamepadFocused;

    public static BrowseItem FromCatalog(QuiverCatalogApp app, IReadOnlyDictionary<string, string> consoleNames)
    {
        var games = string.Join(", ", app.Games.Select(g => g.Title).Where(t => !string.IsNullOrWhiteSpace(t)));

        var kind = app.Consoles.Take(2).Select(id => consoleNames.GetValueOrDefault(id, id))
            .Append(BrowseText.ProjectTypeName(app.ProjectType)).Where(s => s.Length > 0);

        var art = app.LibraryArt;

        // Карточка показывает первые два тега и название консоли.
        var tags = app.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Take(2)
            .Select(t => consoleNames.GetValueOrDefault(t.Trim(), BrowseText.TagLabel(t.Trim()))).ToList();

        var cardKind = tags.Count > 0 ? string.Join(" · ", tags) : string.Join(" · ", kind);
        var basedOn = app.Games.Select(g => g.Title).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        return new(app, null, FirstText(app.ProjectName, app.Name), games.Length > 0 ? games : app.Name,
            FirstText(art?.Header, art?.Capsule, app.Artwork), string.Join(" · ", kind), cardKind, basedOn);
    }

    public static BrowseItem FromList(GameInfo app)
    {
        var title = FirstText(app.Project, app.Name);
        var subtitle = string.Equals(title, app.Name, StringComparison.OrdinalIgnoreCase) ? "" : app.Name;
        var kind = string.Join(" · ", app.Tags.Take(2));

        return new(null, app, title, subtitle, FirstText(app.GameIconUrl), kind, kind,
            subtitle.Length > 0 ? [subtitle] : []);
    }

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";
}

/// <summary>Тексты каталога, переведённые для интерфейса Quiver Launcher.</summary>
public static class BrowseText
{
    public static readonly IReadOnlyList<(string? Id, string Name)> Sorts =
    [
        ("added", "Сначала новые"),
        ("updated", "Недавно обновлённые"),
        ("rating", "С высоким рейтингом"),
        ("name", "По имени А–Я")
    ];

    public static readonly IReadOnlyList<(string? Id, string Name)> Platforms =
    [
        (null, "Все платформы"),
        ("windows", "Windows"),
        ("linux", "Linux"),
        ("macos", "macOS"),
        ("android", "Android"),
        ("ios", "iOS")
    ];

    public static readonly IReadOnlyList<(string? Id, string Name)> ProjectTypes =
    [
        (null, "Все типы проектов"),
        ("port", "Порт"),
        ("tool", "Утилита"),
        ("emulator", "Эмулятор"),
        ("game", "Самостоятельная игра")
    ];

    public static readonly IReadOnlyList<(string? Id, string Name)> AiFilters =
    [
        (null, "Показать все приложения"),
        ("no-generated", "Скрыть приложения, созданные преимущественно ИИ"),
        ("no-ai", "Скрыть приложения с использованием ИИ")
    ];

    public static string? CurrentPlatform =>
        OperatingSystem.IsAndroid() ? "android" : OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : null;

    public static string ProjectTypeName(string? id) =>
        string.IsNullOrEmpty(id) ? "" : ProjectTypes.FirstOrDefault(t => t.Id == id).Name ?? "";

    public static string PlatformNames(IEnumerable<string> ids) => string.Join(", ",
        ids.Select(id => Platforms.FirstOrDefault(p => p.Id == id).Name).Where(n => n != null));

    /// <summary>Отзывы пользователей о работе приложения в одной строке.</summary>
    public static string Score(int runs, int issues, int broken)
    {
        var (label, counts, _) = ScoreParts(runs, issues, broken);
        return label + counts;
    }

    /// <summary>Итоговая оценка, количество отзывов и цвет оценки.</summary>
    public static (string Label, string Counts, string Tone) ScoreParts(int runs, int issues, int broken)
    {
        if (runs + issues + broken == 0)
            return ("Пока нет оценок", "", "muted");

        // Выбираем наиболее частый результат. При равенстве приоритет у более осторожной оценки.
        var max = Math.Max(runs, Math.Max(issues, broken));

        var (label, tone) = broken == max
            ? ("Не запускается", "negative")
            : issues == max
                ? ("Есть проблемы", "caution")
                : ("Работает хорошо", "positive");

        var total = runs + issues + broken;

        if (new[] { runs, issues, broken }.Count(n => n > 0) == 1)
        {
            return (label, $" · {total} {RussianWord(total, "пользователь", "пользователя", "пользователей")}", tone);
        }

        var said = new List<string>();

        if (runs > 0)
            said.Add($"{runs} {RussianWord(runs, "работает хорошо", "работают хорошо", "работают хорошо")}");

        if (issues > 0)
            said.Add($"{issues} {RussianWord(issues, "с проблемой", "с проблемами", "с проблемами")}");

        if (broken > 0)
            said.Add($"{broken} {RussianWord(broken, "не запускается", "не запускаются", "не запускаются")}");

        return (label, " · " + string.Join(", ", said), tone);
    }

    private static string RussianWord(int count, string one, string few, string many)
    {
        var n = Math.Abs(count) % 100;
        if (n >= 11 && n <= 14)
            return many;

        return (n % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }

    // Теги хранятся в нижнем регистре для поиска; здесь задаётся их отображение.
    private static readonly Dictionary<string, string> SpecialTagWords = new()
    {
        ["3ds"] = "3DS", ["gb"] = "GB", ["gba"] = "GBA", ["gbc"] = "GBC", ["gcn"] = "GCN", ["n64"] = "N64", ["nds"] = "NDS",
        ["nes"] = "NES", ["snes"] = "SNES", ["pc"] = "PC", ["ps1"] = "PS1", ["ps2"] = "PS2", ["psp"] = "PSP", ["smd"] = "SMD",
        ["ufc"] = "UFC", ["wwe"] = "WWE", ["wwf"] = "WWF", ["wcw"] = "WCW", ["nwo"] = "nWo", ["x360"] = "X360", ["xmen"] = "X-Men",
        ["yugioh"] = "Yu-Gi-Oh!", ["pokemon"] = "Pokémon", ["initiald"] = "Initial D", ["naomi2"] = "NAOMI 2",
        ["gbarecomp"] = "GBARecomp", ["psxrecomp"] = "PSXRecomp", ["rexglue"] = "ReXGlue", ["einhander"] = "Einhänder",
        ["dantes"] = "Dante's", ["soulcalibur"] = "SoulCalibur", ["co-op"] = "Co-op",
    };

    private static readonly HashSet<string> SmallTagWords = ["a", "an", "and", "in", "of", "on", "the", "to", "vs"];

    /// <summary>Преобразует теги в читаемый вид, сохраняя известные названия и сокращения.</summary>
    public static string TagLabel(string tag)
    {
        if (tag != tag.ToLowerInvariant())
            return tag;

        return string.Join(' ', tag.Split(' ').Select((part, i) =>
            SpecialTagWords.TryGetValue(part, out var special) ? special
            : i > 0 && SmallTagWords.Contains(part) ? part
            : string.Join('-', part.Split('-').Select(piece =>
                piece.Length == 0 ? piece : char.ToUpperInvariant(piece[0]) + piece[1..]))));
    }

    /// <summary>Сортирует варианты запуска игры по отзывам пользователей.</summary>
    public static int ByPlayerFeedback(QuiverCatalogApp a, QuiverCatalogApp b)
    {
        static int Total(QuiverCatalogApp app) => app.Recommended + app.ReportIssues + app.ReportBroken;
        static double Score(QuiverCatalogApp app) => (app.Recommended + app.ReportIssues / 2.0 + 1) / (Total(app) + 2);

        var order = Score(b).CompareTo(Score(a));
        if (order == 0)
            order = Total(b).CompareTo(Total(a));
        if (order == 0)
            order = (b.LastReleaseAt ?? 0).CompareTo(a.LastReleaseAt ?? 0);

        return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Проверяет, содержатся ли все слова запроса в названии игры.</summary>
    public static bool TitleMatches(string title, string search)
    {
        // Поиск игнорирует регистр, диакритические знаки и пунктуацию: «pokemon» найдёт «Pokémon Red».
        static string Fold(string text) => new(text.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant()
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());

        var words = Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var folded = " " + Fold(title).Replace(" ", "") + " " + Fold(title) + " ";

        return words.Length > 0 && words.All(word => folded.Contains(word, StringComparison.Ordinal));
    }

    public static string ReleaseAge(double? releasedAt, DateTimeOffset now)
    {
        if (releasedAt is not { } at)
            return "Релизов нет";

        var age = now - DateTimeOffset.FromUnixTimeMilliseconds((long)at);

        if (age.TotalDays >= 365)
            return $"Без релизов более {(int)(age.TotalDays / 365)} {RussianWord((int)(age.TotalDays / 365), "года", "лет", "лет")}";

        return "Обновлено " + (age.TotalDays >= 30
            ? $"{(int)(age.TotalDays / 30)} {RussianWord((int)(age.TotalDays / 30), "месяц", "месяца", "месяцев")} назад"
            : age.TotalDays >= 1
                ? $"{(int)age.TotalDays} {RussianWord((int)age.TotalDays, "день", "дня", "дней")} назад"
                : age.TotalHours >= 1
                    ? $"{(int)age.TotalHours} {RussianWord((int)age.TotalHours, "час", "часа", "часов")} назад"
                    : "только что");
    }

    public static string ReviewResult(string result) => result switch
    {
        "runs" => "Работает хорошо",
        "issues" => "Работает с проблемами",
        "broken" => "Не запускается",
        _ => result,
    };
}
 