
using QuiverLauncher.Services;

namespace QuiverLauncher.ViewModels;

/// <summary>Карточка каталога приложений, на которую можно перейти с помощью контроллера: приложение или найденная игра.</summary>
public interface IBrowseCard
{
    bool IsGamepadFocused { get; set; }
}

/// <summary>Найденная игрой поиском оригинальная игра: изображение, название и количество приложений для её запуска.</summary>
public sealed class BrowseGame(string slug, string title, string? art, int ways) : ObservableViewModel, IBrowseCard
{
    private bool _isGamepadFocused;

    public string Slug { get; } = slug;
    public string Title { get; } = title;
    public string? Art { get; } = art;
    public int Ways { get; } = ways;

    public string WaysText => Ways switch
    {
        1 => "1 способ запуска →",
        2 or 3 or 4 => $"{Ways} способа запуска →",
        _ => $"{Ways} способов запуска →"
    };

    public bool IsGamepadFocused { get => _isGamepadFocused; set => Set(ref _isGamepadFocused, value); }

    /// <summary>Обложка игры или, если её нет, другое изображение игры.</summary>
    public static string? ArtFor(QuiverCatalogGameInfo game) =>
        new[] { game.LibraryArt?.Capsule, game.Artwork, game.LibraryArt?.Header }.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
}
