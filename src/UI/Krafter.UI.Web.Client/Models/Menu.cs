namespace Krafter.UI.Web.Client.Models;

public class Menu
{
    public Menu()
    {
        Children = new List<Menu>();
        Tags = new List<string>();
    }

    public bool New { get; set; }
    public bool Updated { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Path { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public bool Expanded { get; set; }
    public string Permission { get; set; } = string.Empty;
    public IEnumerable<Menu>? Children { get; set; }
    public IEnumerable<string> Tags { get; set; }
}
