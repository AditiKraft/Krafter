using Krafter.Shared.Common.Models;
using Krafter.UI.Web.Client.Common.Components.Dialogs;

namespace Krafter.UI.Web.Client.Infrastructure.Services;

public class CommonService(DialogService dialogService)
{
    public async Task Delete(DeleteRequestInput input, string heading)
    {
        await dialogService.OpenAsync<DeleteDialog>(heading,
            new Dictionary<string, object> { { "DeleteRequestInput", input } },
            new DialogOptions { Width = "50vw", Resizable = true, Draggable = true, Top = "5vh" });
    }
}
