using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HaladeHighSchool.Api.Services;

public interface IPdfReportCardService
{
    Task<byte[]?> GenerateAsync(int studentId, CancellationToken cancellationToken = default);
}

public class PdfReportCardService : IPdfReportCardService
{
    private readonly ApplicationDbContext _db;
    private readonly IReportCardService _reportCards;

    public PdfReportCardService(ApplicationDbContext db, IReportCardService reportCards)
    {
        _db = db;
        _reportCards = reportCards;
    }

    public async Task<byte[]?> GenerateAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var card = await _reportCards.BuildAsync(studentId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
        {
            return null;
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(35);
                page.Size(PageSizes.A4);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("School Management System")
                            .FontSize(20)
                            .SemiBold();
                        column.Item().Text("Academic Report Card")
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(140).AlignRight().Column(col =>
                    {
                        col.Item().Text(card.AcademicYear)
                            .FontSize(11)
                            .SemiBold();
                        col.Item().Text($"Pass mark: {card.PassMarkPercentage:0.##}%")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(inner =>
                    {
                        inner.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Student: {student.User?.FullName ?? card.StudentName}");
                            row.RelativeItem().Text($"Student ID: {card.StudentIdNumber}");
                        });

                        inner.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Grade: {card.GradeLevelName}");
                            row.RelativeItem().Text($"Section: {card.SectionName}");
                        });

                        if (card.AverageTotal is decimal average)
                        {
                            inner.Item().Text($"Average total: {average:F2}");
                        }
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Subject");
                            header.Cell().Element(CellStyle).Text("Quiz");
                            header.Cell().Element(CellStyle).Text("Test");
                            header.Cell().Element(CellStyle).Text("Mid");
                            header.Cell().Element(CellStyle).Text("Final");
                            header.Cell().Element(CellStyle).Text("Total");
                            header.Cell().Element(CellStyle).Text("Status");
                        });

                        foreach (var subject in card.Subjects)
                        {
                            table.Cell().Element(CellStyle).Text(subject.SubjectName);
                            table.Cell().Element(CellStyle).Text(subject.QuizScore?.ToString("F1") ?? "-");
                            table.Cell().Element(CellStyle).Text(subject.TestScore?.ToString("F1") ?? "-");
                            table.Cell().Element(CellStyle).Text(subject.MidExamScore?.ToString("F1") ?? "-");
                            table.Cell().Element(CellStyle).Text(subject.FinalExamScore?.ToString("F1") ?? "-");
                            table.Cell().Element(CellStyle).Text($"{subject.TotalScore:F1}");
                            table.Cell().Element(CellStyle).Text(subject.Status);
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"Generated on {DateTime.UtcNow:yyyy-MM-dd}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();

        return bytes;
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(6)
            .AlignCenter();
    }
}
