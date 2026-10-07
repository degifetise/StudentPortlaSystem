namespace HaladeHighSchool.Api.Models;

public class SmartCard
{
    public Guid CardId { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string CardUID { get; set; } = string.Empty;

    public string QrTokenHash { get; set; } = string.Empty;

    public string CardType { get; set; } = string.Empty;

    public string Status { get; set; } = "ACTIVE";

    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    public DateTime ExpirationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}

public class SmartIDScanLog
{
    public long Id { get; set; }

    public string ScannerUserId { get; set; } = string.Empty;

    public string? ScannedUserId { get; set; }

    public string? ScanLocation { get; set; }

    public DateTime ScanTimestamp { get; set; } = DateTime.UtcNow;

    public string ScanStatus { get; set; } = string.Empty;
}
