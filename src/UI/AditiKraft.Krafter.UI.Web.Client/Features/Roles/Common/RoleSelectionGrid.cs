namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles.Common;

public sealed class RoleSelectionGrid : RadzenDropDownDataGrid<IEnumerable<string>>
{
    private readonly Dictionary<string, RoleDto> selectedRoles = new(StringComparer.Ordinal);

    [Parameter] public IEnumerable<RoleDto> SelectionData { get; set; } = [];

    protected override void SelectItemFromValue(object value)
    {
        base.SelectItemFromValue(value);
        if (value is not IEnumerable<string> values)
        {
            selectedRoles.Clear();
            return;
        }

        string[] ids = values.Distinct(StringComparer.Ordinal).ToArray();
        var selectedIds = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (RoleDto role in SelectionData.Where(role => selectedIds.Contains(role.Id)))
        {
            selectedRoles.TryAdd(role.Id, role);
        }
        foreach (RoleDto role in (Data?.Cast<RoleDto>() ?? []).Where(role => selectedIds.Contains(role.Id)))
        {
            selectedRoles[role.Id] = role;
        }
        foreach (string id in selectedRoles.Keys.Where(id => !selectedIds.Contains(id)).ToArray())
        {
            selectedRoles.Remove(id);
        }

        // Radzen 8.3.2 rebuilds native selection changes from selectedItems. Its
        // default lookup only resolves the current page, so represent every bound
        // ID here to preserve off-page roles and let native deselect/Clear remove them.
        selectedItems = ids.Select(id => selectedRoles.TryGetValue(id, out RoleDto? role)
                ? role : new RoleDto { Id = id, Name = id })
            .Cast<object>().ToHashSet(ItemComparer);
    }
}
