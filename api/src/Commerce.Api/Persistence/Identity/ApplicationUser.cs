using Microsoft.AspNetCore.Identity;

namespace Commerce.Api.Persistence.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class ApplicationRole : IdentityRole<Guid>
{
}

public static class AppRoles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}