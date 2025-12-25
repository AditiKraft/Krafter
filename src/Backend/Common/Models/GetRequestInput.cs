using System.Reflection;

namespace Backend.Common.Models;

public class GetRequestInput : IPagedResultRequest
{
    public string? Id { get; set; }
    public bool History { get; set; }
    public bool IsDeleted { get; set; }
    public int MaxResultCount { get; set; } = 10;
    public int SkipCount { get; set; }
    public string? Query { get; set; }
    public string? OrderBy { get; set; } = "CreatedOn desc";
    public string? Filter { get; set; }
}
//public class GetRequestInput : IPagedResultRequest
//{
//    public string? Id { get; set; }
//    public bool History { get; set; }
//    public bool IsDeleted { get; set; }
//    public int MaxResultCount { get; set; } = 10;
//    public int SkipCount { get; set; }
//    public string? Query { get; set; }
//    public string? OrderBy { get; set; } = "CreatedOn desc";
//    public string? Filter { get; set; }

//    public int AssociatedEntityType { get; set; }
//    public string? AssociationEntityId { get; set; }
//    public List<string> AssociatedEntityIds { get; set; }
//    public List<int> RecordStates { get; set; }
//    public List<int> SubDocumentTypes { get; set; }

//    //Order Statuses
//    public List<int> OrderStatuses { get; set; }

//    // public ProductType ProductType { get; set; }
//    public int? MaxLevel { get; set; }

//    public int? TargetLevel { get; set; }
//    public decimal? Quantity { get; set; }
//    public string ProductId { get; set; }

//    //isPublisehd
//    public bool? IsPublished { get; set; }

//    public static ValueTask<GetRequestInput?> BindAsync(HttpContext context, ParameterInfo parameter)
//    {
//        string associationEntityId = context.Request.Query["AssociationEntityId"];
//        int.TryParse(context.Request.Query["AssociatedEntityType"], out var associationEntity);

//        string orderBy = context.Request.Query["OrderBy"];
//        string filter = context.Request.Query["Filter"];
//        string query = context.Request.Query["Query"];
//        string id = context.Request.Query["id"];

//        string productId = context.Request.Query["ProductId"];
//        int.TryParse(context.Request.Query["SkipCount"], out var skipCount);
//        int.TryParse(context.Request.Query["MaxResultCount"], out var maxResultCount);
//        bool.TryParse(context.Request.Query["History"], out var history);
//        bool.TryParse(context.Request.Query["IsDeleted"], out var isDeleted);
//        List<int> recordStates = new();
//        if (!string.IsNullOrWhiteSpace(context.Request.Query["RecordStates"]))
//        {
//            var array = context.Request.Query["RecordStates"].ToArray();
//            foreach (var s in array)
//            {
//                int.TryParse(s, out var state);
//                if (state > 0)
//                {
//                    recordStates.Add(state);
//                }
//            }
//        }

//        List<int> subDocumentTypes = new();
//        if (!string.IsNullOrWhiteSpace(context.Request.Query["SubDocumentTypes"]))
//        {
//            var array = context.Request.Query["SubDocumentTypes"].ToArray();
//            foreach (var s in array)
//            {
//                int.TryParse(s, out var state);
//                if (state > 0)
//                {
//                    subDocumentTypes.Add(state);
//                }
//            }
//        }
//        List<string> associatedEntityIds = new();
//        if (!string.IsNullOrWhiteSpace(context.Request.Query["AssociatedEntityIds"]))
//        {
//            associatedEntityIds = context.Request.Query["AssociatedEntityIds"].ToList();
//        }

//        List<int> orderStatus = new();

//        if (!string.IsNullOrWhiteSpace(context.Request.Query["OrderStatuses"]))
//        {
//            var array = context.Request.Query["OrderStatuses"].ToArray();
//            foreach (var s in array)
//            {
//                int.TryParse(s, out var state);
//                if (state > 0)
//                {
//                    orderStatus.Add(state);
//                }
//            }
//        }

//        int? maxLevel = null;
//        int? targetLevel = null;
//        if (!string.IsNullOrWhiteSpace(context.Request.Query["MaxLevel"]))
//        {
//            var array = context.Request.Query["MaxLevel"];
//            int.TryParse(array, out var level);
//            if (level > 0)
//            {
//                maxLevel = level;
//            }
//        }

//        if (!string.IsNullOrWhiteSpace(context.Request.Query["TargetLevel"]))
//        {
//            var array = context.Request.Query["TargetLevel"];
//            int.TryParse(array, out var level);
//            if (level > 0)
//            {
//                targetLevel = level;
//            }
//        }
//        decimal? quantity = null;
//        if (!string.IsNullOrWhiteSpace(context.Request.Query["Quantity"]))
//        {
//            var array = context.Request.Query["Quantity"];
//            decimal.TryParse(array, out var quantityValue);
//            if (quantityValue > 0)
//            {
//                quantity = quantityValue;
//            }
//        }
//        var result = new GetRequestInput
//        {
//            Query = query,
//            Filter = filter,
//            OrderBy = orderBy,
//            SkipCount = skipCount,
//            MaxResultCount = maxResultCount,
//            History = history,
//            Id = id,
//            IsDeleted = isDeleted,

//            AssociatedEntityType = associationEntity,
//            AssociationEntityId = associationEntityId,
//            AssociatedEntityIds = associatedEntityIds,
//            RecordStates = recordStates,
//            SubDocumentTypes = subDocumentTypes,
//            MaxLevel = maxLevel,
//            TargetLevel = targetLevel,
//            Quantity = quantity,
//            ProductId = productId
//        };
//        if (result.MaxResultCount == 0)
//        {
//            result.MaxResultCount = 10;
//        }

//        if (string.IsNullOrWhiteSpace(result.OrderBy))
//        {
//            result.OrderBy = "CreatedOn desc";
//        }

//        if (!string.IsNullOrWhiteSpace(result.Filter))
//        {
//            result.Filter = result.Filter.Trim();
//        }
//        return ValueTask.FromResult<GetRequestInput?>(result);
//    }
//}

public interface IPagedResultRequest
{
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; }
}
