using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Infrastructure;

namespace WupmGui.ViewModels;

public class MarketplaceViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<MarketplacePlugin> Items { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set => Set(ref _searchQuery, value);
    }

    public MarketplaceViewModel(IWupmApiClient api)
    {
        _api = api;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
    }

    public ICommand SearchCommand { get; }

    private async Task SearchAsync(CancellationToken ct)
    {
        StatusMessage = "Searching marketplace...";
        try
        {
            var results = await _api.MarketplaceSearchAsync(SearchQuery, ct);
            Items.Clear();
            foreach (var item in results)
                Items.Add(item);

            StatusMessage = $"Found {Items.Count} items";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
    }
}
