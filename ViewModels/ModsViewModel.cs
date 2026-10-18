
using System.Collections.ObjectModel;
using QuiverLauncher.Models;
using QuiverLauncher.Services.Mods;

namespace QuiverLauncher.ViewModels;

/// <summary>Каталог модификаций, фильтрация и состояние списка без ссылок на элементы интерфейса.</summary>
public sealed class ModsViewModel : ObservableViewModel
{
    public ObservableCollection<ModListItem> Rows { get; } = [];
    private string _status = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }
    internal int OpenVersion;
    internal GameInfo? Game;
    internal List<ModPackage> Catalog = [];
    internal readonly Dictionary<string, ModPackage> KnownPackages =
        new(StringComparer.OrdinalIgnoreCase);
    internal readonly HashSet<string> OrphanEnrichAttempted =
        new(StringComparer.OrdinalIgnoreCase);
    internal List<ModListItem> AllItems = [];
    internal string Tab = "Browse";
    internal string? SourceFilterKey;
    internal string SearchText = string.Empty;
    internal string SortBy = ModListSorter.InstalledFirst;
    internal bool IncludeNsfw;
    internal ModBrowseSession? BrowseSession;
    internal bool UsesPagedBrowse;
    internal bool IsLoadingMore;
    private bool _listIsLoading;
    public bool ListIsLoading { get => _listIsLoading; internal set => Set(ref _listIsLoading, value); }
    private string _loadingCaption = "Загрузка модификаций…";
    public string LoadingCaption { get => _loadingCaption; internal set => Set(ref _loadingCaption, value); }
    internal CancellationTokenSource? BackgroundLoadCts;
    internal CancellationTokenSource? SearchDebounceCts;

    internal int Open(GameInfo game, bool includeNsfw)
    {
        Reset();
        Game = game;
        IncludeNsfw = includeNsfw;
        Tab = "Browse";
        SourceFilterKey = null;
        SearchText = string.Empty;
        return OpenVersion;
    }

    internal void Close() => Reset();

    private void Reset()
    {
        OpenVersion++;
        Game = null;
        Catalog = [];
        KnownPackages.Clear();
        OrphanEnrichAttempted.Clear();
        AllItems = [];
        BrowseSession = null;
        UsesPagedBrowse = false;
        ListIsLoading = false;
        Rows.Clear();
        Status = string.Empty;
    }

    public void ApplyFilters()
    {
        IEnumerable<ModListItem> query = AllItems;

        // Удалённый поиск уже применяет фильтр NSFW через API, если он поддерживается.
        // Дополнительно скрываем такие модификации для GameBanana и оставшихся записей списка.
        if (!IncludeNsfw)
            query = query.Where(i => !i.Package.HasContentRating);

        if (!string.IsNullOrEmpty(SourceFilterKey) && BrowseSession?.IsSearch != true)
            query = query.Where(i =>
                string.Equals($"{i.ProviderId}|{i.SourceKey}", SourceFilterKey, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(Tab, "Installed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(i => i.Status is ModInstallStatus.Installed or ModInstallStatus.UpdateAvailable);

        // Локальная фильтрация по тексту используется, если не показаны результаты удалённого поиска,
        // либо если открыта вкладка установленных модификаций.
        var useLocalSearchFilter = !string.IsNullOrWhiteSpace(SearchText) &&
                                   (BrowseSession?.IsSearch != true ||
                                    string.Equals(Tab, "Installed", StringComparison.OrdinalIgnoreCase));
        if (useLocalSearchFilter)
        {
            var term = SearchText;
            query = query.Where(i =>
                i.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Owner.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Package.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var rows = ModListSorter.Sort(query, SortBy);
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);

    }
}
