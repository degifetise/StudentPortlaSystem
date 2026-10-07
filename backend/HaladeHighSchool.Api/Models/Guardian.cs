namespace HaladeHighSchool.Api.Models;

public class Guardian
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}

public class StudentGuardian
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int GuardianId { get; set; }

    public string? Relationship { get; set; }

    public bool IsPrimaryContact { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Student? Student { get; set; }

    public Guardian? Guardian { get; set; }
}
