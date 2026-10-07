namespace HaladeHighSchool.Api.Services;

public interface IReportPdfService
{
    Task<byte[]?> GenerateMyResultsAsync(string? userId, CancellationToken cancellationToken);

    Task<byte[]> GenerateStudentsRosterAsync(
        int? gradeLevelId,
        int? sectionId,
        CancellationToken cancellationToken);

    Task<byte[]> GenerateTeachersRosterAsync(CancellationToken cancellationToken);

    Task<byte[]?> GenerateTeacherSectionRosterAsync(
        string? userId,
        int sectionId,
        CancellationToken cancellationToken);
}
