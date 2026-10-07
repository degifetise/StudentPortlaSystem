namespace HaladeHighSchool.Api.Models;

public class FeeInvoice
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string? Term { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsVoided { get; set; }
    public DateTime CreatedAt { get; set; }
    public Student? Student { get; set; }
    public ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();
}

public class FeePayment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal AmountPaid { get; set; }
    public string? PaymentMethod { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime PaidAt { get; set; }
    public FeeInvoice? Invoice { get; set; }
}
