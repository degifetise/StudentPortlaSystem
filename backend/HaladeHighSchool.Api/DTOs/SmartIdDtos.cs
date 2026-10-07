namespace HaladeHighSchool.Api.DTOs;

/// <summary>Administrator view of a user and their current Smart ID issuance state.</summary>
public sealed record SmartIDAdminUserResponse(
    string UserId,
    string FullName,
    string Role,
    string Identifier,
    string? DepartmentOrGrade,
    string? Section,
    string? PhotoUrl,
    bool CardGenerated,
    SmartIDCardDetails? Card);

/// <summary>Administrator changes to cardholder profile and card security settings.</summary>
public sealed record UpdateSmartIDCardRequest
{
    public string? FullName { get; init; }
    public string? Status { get; init; }
    public bool RotateQrToken { get; init; }
}

public record SmartIDCardDetails
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? GradeLevel { get; init; }
    public string? Section { get; init; }
    public string AcademicYear { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? EmergencyContact { get; init; }
    public string? BloodGroup { get; init; }
    public string? DigitalSignatureUrl { get; init; }
    public string CardUID { get; init; } = string.Empty;
    public string CardStatus { get; init; } = string.Empty;
    public DateTime IssuedDate { get; init; }
    public DateTime ExpirationDate { get; init; }
}

public record BatchSmartIDRequest
{
    public IReadOnlyList<string>? UserIds { get; init; }
}

public record UpdateSmartIDCardStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public record GenerateSmartIDTokenResponse(string Token, DateTime ExpiresAt);

public sealed record SmartIDPaymentStatusResponse(
    bool PaymentRequired,
    bool IsPaid,
    bool CardGenerated,
    decimal AmountDue,
    decimal AmountPaid,
    decimal Balance);

public record VerifySmartIDScanRequest
{
    public string Code { get; init; } = string.Empty;
    public string? ScanLocation { get; init; }
}

public record VerifySmartIDScanResponse
{
    public string Status { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public SmartIDCardDetails? Card { get; init; }
}
