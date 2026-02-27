namespace AditiKraft.Krafter.UI.Web.Client.Common.Models;

public class Menu
{
    public Menu()
    {
        Children = new List<Menu>();
        Tags = new List<string>();
    }

    public bool New { get; set; }
    public bool Updated { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Expanded { get; set; }
    public string Permission { get; set; } = string.Empty;
    public IEnumerable<Menu>? Children { get; set; }
    public IEnumerable<string> Tags { get; set; }
}

