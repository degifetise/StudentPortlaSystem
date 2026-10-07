namespace HaladeHighSchool.Api.Models;

/// <summary>Academic grade level from Nursery through Grade 12.</summary>
public class GradeLevel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Academic progression level: Nursery (-3) through Grade 12 (12).</summary>
    public int Level { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
