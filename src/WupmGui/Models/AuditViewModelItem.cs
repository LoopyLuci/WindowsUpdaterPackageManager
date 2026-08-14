using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.Models;

public sealed class AuditViewModelItem
{
    public AuditViewModelItem(AuditEntry entry)
    {
        Entry = entry;
    }

    public AuditEntry Entry { get; }
    public string Timestamp => Entry.Timestamp.ToString("g");
    public string Action => Entry.Action;
    public string PackageId => Entry.PackageId;
    public string Success => Entry.Success ? "Yes" : "No";
}
