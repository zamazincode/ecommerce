namespace Commerce.Domain.Users;

public class Address
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string City { get; set; } = null!;
    public string District { get; set; } = null!;
    public string FullAddress { get; set; } = null!;
    public bool IsDefault { get; set; }
}