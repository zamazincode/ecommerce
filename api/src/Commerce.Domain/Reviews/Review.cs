using Commerce.Domain.Catalog;

namespace Commerce.Domain.Reviews;

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }          // 1-5
    public string? Comment { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
}

public class Favorite
{
    public Guid UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
}

public class SearchLog
{
    public int Id { get; set; }
    public string Term { get; set; } = null!;
    public int ResultCount { get; set; }
    public DateTime CreatedAt { get; set; }
}