using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Models;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class UpdatesViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;
    public ObservableCollection<UpdateItem> Items { get; } = new();

    public UpdatesViewModel(IWupmApiClient api)
    {
        _api = api;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ICommand RefreshCommand { get; }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var updates = await _api.GetUpdatesAsync("10.0");
            Items.Clear();
            foreach (var u in updates)
            {
                Items.Add(u);
            }
        }
        catch
        {
            // ignore refresh failures for now
        }
    }
}
