
using QuiverLauncher.Models;
using QuiverLauncher.Services;

namespace QuiverLauncher.ViewModels;

/// <summary>Отзывы игроков в формате сайта: кто, когда и на чём играл, результат и комментарий.</summary>
public sealed record BrowseReviewLine(string Author, string Meta, string Result, string Tone, string Body)
{
    public string Initial => Author.Length > 0 ? Author[..1].ToUpperInvariant() : "?";
    public bool HasBody => Body.Length > 0;
    public bool Positive => Tone == "positive";
    public bool Caution => Tone == "caution";
    public bool Negative => Tone == "negative";
}

/// <summary>
/// Подробная информация о приложении в каталоге: сведения с сайта, последние отзывы игроков
/// (только для чтения; отзывы публикуются на quiverlauncher.com) и файл README.
/// </summary>
public sealed class BrowseDetailsViewModel(QuiverCatalogClient client, Func<QuiverCatalogApp, QuiverCatalogProject, GameInfo> toLibraryEntry,
    Func<GameInfo, CancellationToken, Task<DocumentContent>> repositoryReadme,
    Func<GameInfo, CancellationToken, Task<IReadOnlyList<QuiverCatalogRelease>>>? repositoryReleases = null) : ObservableViewModel, IDisposable
{
    public const int ReviewCount = 10;
    private int _generation;
    private BrowseItem? _item;
    private GameInfo? _entry;
    private QuiverCatalogProject? _project;
    private IReadOnlyList<QuiverCatalogRelease>? _releases;
    private bool _releasesMore;
    private string _releasesStatus = "";
    private IReadOnlyList<BrowseReviewLine> _reviews = [];
    private string _reviewsStatus = "";
    private string _error = "";

    public BrowseItem? Item { get => _item; private set => Set(ref _item, value); }

    /// <summary>Приложение в формате записи библиотеки; доступно после определения его источника в каталоге.</summary>
    public GameInfo? Entry { get => _entry; private set => Set(ref _entry, value); }

    /// <summary>Автор приложения и источник проекта после загрузки страницы.</summary>
    public QuiverCatalogProject? Project { get => _project; private set => Set(ref _project, value); }
    public IReadOnlyList<BrowseReviewLine> Reviews { get => _reviews; private set => Set(ref _reviews, value); }

    /// <summary>История выпусков приложения, отсортированная от новых к старым.</summary>
    public IReadOnlyList<QuiverCatalogRelease>? Releases { get => _releases; private set => Set(ref _releases, value); }

    /// <summary>На сайте есть более старые выпуски, чем показанные.</summary>
    public bool ReleasesMore { get => _releasesMore; private set => Set(ref _releasesMore, value); }
    public string ReleasesStatus { get => _releasesStatus; private set => Set(ref _releasesStatus, value); }
    public string ReviewsStatus { get => _reviewsStatus; private set => Set(ref _reviewsStatus, value); }
    public string Error { get => _error; private set => Set(ref _error, value); }
    public DocumentViewModel Readme { get; } = new();
    public string? ReviewUrl => Item?.App is { } app ? QuiverCatalogClient.ReviewPageUrl(app.Slug) : null;

    public async Task OpenAsync(BrowseItem item, CancellationToken token)
    {
        var generation = ++_generation;
        Item = item;
        Entry = item.ListApp;
        Project = null;
        Releases = null;
        ReleasesMore = false;
        ReleasesStatus = "";
        Error = "";
        Reviews = [];
        ReviewsStatus = item.App == null ? "" : "Загрузка отзывов…";
        var readme = Readme.OpenAsync(item.Title, "Загрузка README…", ct => LoadReadmeAsync(item, ct), token);
        if (item.App is { } app)
            await Task.WhenAll(LoadDetailAsync(app, generation, token), LoadReviewsAsync(app, generation, token));
        await readme;
    }

    public void Close()
    {
        ++_generation;
        Readme.Cancel();
        Item = null;
        Entry = null;
        Project = null;
        Releases = null;
        ReleasesStatus = "";
        Reviews = [];
        ReviewsStatus = "";
        Error = "";
    }

    public void Dispose() => Close();

    /// <summary>Есть ли выпуски приложения: в каталоге или, если его там нет, в репозитории.</summary>
    public bool HasReleases => Item?.App != null || (repositoryReleases != null && !string.IsNullOrWhiteSpace(Item?.ListApp?.Repository));

    /// <summary>Загружает историю выпусков при первом открытии вкладки «Релизы».</summary>
    public async Task LoadReleasesAsync(CancellationToken token)
    {
        if (!HasReleases || Releases != null || ReleasesStatus.Length > 0) return;
        if (Item?.App is not { } app)
        {
            await LoadRepositoryReleasesAsync(Item!.ListApp!, token);
            return;
        }
        var generation = _generation;
        ReleasesStatus = "Загрузка релизов…";
        try
        {
            var page = await client.GetReleaseHistoryAsync(app.Slug, token);
            if (generation != _generation) return;
            Releases = page.Items;
            ReleasesMore = !page.IsDone;
            ReleasesStatus = page.Items.Count == 0 ? "История выпусков ещё не добавлена в каталог. Пока проверьте репозиторий проекта." : "";
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            if (generation == _generation) ReleasesStatus = $"Не удалось загрузить релизы. {ex.Message}";
        }
    }

    // Приложение отсутствует в каталоге: загружаем последние выпуски и примечания прямо из его репозитория.
    private async Task LoadRepositoryReleasesAsync(GameInfo app, CancellationToken token)
    {
        var generation = _generation;
        ReleasesStatus = "Загрузка релизов…";
        try
        {
            var releases = await repositoryReleases!(app, token);
            if (generation != _generation) return;
            Releases = releases;
            ReleasesMore = false;
            ReleasesStatus = releases.Count == 0 ? "В репозитории этого приложения нет релизов." : "";
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            if (generation == _generation) ReleasesStatus = $"Не удалось загрузить релизы из репозитория приложения. {ex.Message}";
        }
    }

    private async Task LoadDetailAsync(QuiverCatalogApp app, int generation, CancellationToken token)
    {
        try
        {
            var detail = await client.GetDetailAsync(app.Slug, token);
            if (generation != _generation) return;
            Project = detail.Project;
            Entry = toLibraryEntry(detail.Entry, detail.Project);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            if (generation == _generation) Error = $"Не удалось загрузить сведения об этом приложении с quiverlauncher.com, поэтому сейчас его нельзя добавить. {ex.Message}";
        }
    }

    private async Task LoadReviewsAsync(QuiverCatalogApp app, int generation, CancellationToken token)
    {
        try
        {
            var page = await client.GetReviewsAsync(app.Slug, ReviewCount, token);
            if (generation != _generation) return;
            Reviews = page.Items.Select(ToLine).ToList();
            ReviewsStatus = "";
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            if (generation == _generation) ReviewsStatus = $"Не удалось загрузить отзывы. {ex.Message}";
        }
    }

    private async Task<DocumentContent> LoadReadmeAsync(BrowseItem item, CancellationToken token)
    {
        try
        {
            if (item.App == null)
                return item.ListApp == null ? new("Файл README отсутствует.", IsMarkdown: false) : await repositoryReadme(item.ListApp, token);
            var readme = await client.GetReadmeAsync(item.App.Slug, token);
            return string.IsNullOrWhiteSpace(readme?.Markdown)
                ? new(string.IsNullOrWhiteSpace(item.App.Description) ? "У этого приложения нет файла README." : item.App.Description, IsMarkdown: false)
                : new(readme.Markdown, readme.RawBase);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            return new($"Не удалось загрузить README. {ex.Message}", IsMarkdown: false);
        }
    }

    internal static BrowseReviewLine ToLine(QuiverCatalogReview review)
    {
        var meta = new List<string>();
        if (review.CreatedAt > 0) meta.Add(DateTimeOffset.FromUnixTimeMilliseconds((long)review.CreatedAt).LocalDateTime.ToString("d MMM yyyy"));
        if (review.Platform is { Length: > 0 } platform) meta.Add(BrowseText.PlatformNames([platform]));
        if (review.Version is { Length: > 0 } version) meta.Add($"проверено на версии {version}");
        var tone = review.Result switch { "runs" => "positive", "issues" => "caution", "broken" => "negative", _ => "" };
        return new(string.IsNullOrWhiteSpace(review.Author) ? "Игрок" : review.Author.Trim(),
            string.Join(" · ", meta.Where(p => p.Length > 0)), BrowseText.ReviewResult(review.Result), tone, review.Body.Trim());
    }
}
