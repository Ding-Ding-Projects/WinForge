namespace DewEncryption.Core;

public sealed class DewSelectionService
{
    private readonly List<DewPathItem> selectedPaths = [];

    public IReadOnlyList<DewPathItem> SelectedPaths => selectedPaths;

    public bool AddPath(string path, string kind)
    {
        if (selectedPaths.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        selectedPaths.Add(new DewPathItem(path, kind));
        return true;
    }

    public void RemovePaths(IEnumerable<DewPathItem> items)
    {
        foreach (DewPathItem item in items.ToArray())
        {
            selectedPaths.Remove(item);
        }
    }
}
