using Krafter.UI.Web.Client.Common.Models;

namespace Krafter.UI.Web.Client.Common.Extensions;

public static class RequestInputExtensions
{
    public static string GetIdentifierBasedOnPlacement(this GetRequestInput requestInput, string defaultName)
    {
        if (requestInput is null)
        {
            return defaultName;
        }

        string res = string.Empty;
        if (requestInput.AssociatedEntityType != 0)
        {
            res = !string.IsNullOrWhiteSpace(requestInput.AssociationEntityId)
                ? $"{requestInput.AssociatedEntityType}.{requestInput.AssociationEntityId}"
                : requestInput.AssociatedEntityType.ToString();
        }

        res = !string.IsNullOrWhiteSpace(requestInput.AssociationEntityId)
            ? $"{requestInput.AssociatedEntityType}.{requestInput.AssociationEntityId}"
            : defaultName;

        res = defaultName + "." + res;
        return res;
    }
}
