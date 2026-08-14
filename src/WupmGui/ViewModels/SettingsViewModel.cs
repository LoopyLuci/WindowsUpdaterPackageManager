namespace WupmGui.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _version = "1.0.0";
    public string Version
    {
        get => _version;
        set => Set(ref _version, value);
    }

    public SettingsViewModel()
    {
    }
}
