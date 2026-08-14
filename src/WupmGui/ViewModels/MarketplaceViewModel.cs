using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class MarketplaceViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<object> Items { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
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
        Items.Clear();
        StatusMessage = $"Found {Items.Count} items";
    }
}
