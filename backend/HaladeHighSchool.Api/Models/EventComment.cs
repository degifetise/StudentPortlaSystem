namespace HaladeHighSchool.Api.Models;

public class EventComment
{
    public long Id { get; set; }
    public int EventId { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public long? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Event Event { get; set; } = null!;
    public ApplicationUser? User { get; set; }
    public Student? Student { get; set; }
    public EventComment? ParentComment { get; set; }
    public ICollection<EventComment> Replies { get; set; } = new List<EventComment>();
}
