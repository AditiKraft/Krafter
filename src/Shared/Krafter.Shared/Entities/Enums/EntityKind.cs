namespace Krafter.Shared.Entities.Enums;

public enum EntityKind
{
    None = 000,
    Tenant = 001,

    // Group: ASPNET Auth Entities (100-199)
    KrafterRole = 100,

    KrafterRoleClaim = 110,
    KrafterUser = 120,
    KrafterUserClaim = 130,
    KrafterUserRole = 140,
    UserRefreshToken = 150,
    IdentityUserLogin1 = 160,
    IdentityUserToken1 = 170,

    Course = 200,
    Chapter = 210,
    Content = 220,
    ShoppingCarts = 300,
    CartItems = 310,
    Orders = 320,
    OrderItems = 330,
    Payments = 340,
    CourseEnrollments = 350,
    ContentProgresses = 360
}
