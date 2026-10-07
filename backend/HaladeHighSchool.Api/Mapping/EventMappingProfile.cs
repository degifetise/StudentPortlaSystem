using AutoMapper;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.Mapping;

public class EventMappingProfile : Profile
{
    public EventMappingProfile()
    {
        CreateMap<Event, EventDto>()
            .ForMember(d => d.BannerUrl, o => o.MapFrom(s => s.BannerImageUrl))
            .ForMember(d => d.Location, o => o.MapFrom(s => s.Venue))
            .ForMember(d => d.StartDate, o => o.MapFrom(s => s.StartsAt))
            .ForMember(d => d.EndDate, o => o.MapFrom(s => s.EndsAt))
            .ForMember(d => d.MaxAttendees, o => o.MapFrom(s => s.MaxStudents))
            .ForMember(d => d.TargetStakeholders, o => o.MapFrom(s => StakeholdersFor(s.TargetRole)))
            .ForMember(d => d.TargetGradeIds, o => o.MapFrom(s => ParseIds(s.TargetGradeIdsCsv)))
            .ForMember(d => d.TargetSectionIds, o => o.MapFrom(s => ParseIds(s.TargetSectionIdsCsv)))
            .ForMember(d => d.IsActive, o => o.MapFrom(s => s.IsPublished && s.Status != EventStatuses.Canceled && s.Status != EventStatuses.Completed))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status))
            .ForMember(d => d.CurrentRegistrationsCount, o => o.MapFrom(s => s.Registrations.Count(r => r.Status == EventRegistrationStatuses.Confirmed)))
            .ForMember(d => d.IsRegistrationOpen, o => o.MapFrom(s =>
                s.IsPublished && s.Status != EventStatuses.Canceled && s.Status != EventStatuses.Completed &&
                (!s.RegistrationDeadline.HasValue || DateTime.UtcNow <= s.RegistrationDeadline.Value) &&
                s.Registrations.Count(r => r.Status == EventRegistrationStatuses.Confirmed) < (s.MaxStudents ?? int.MaxValue)));

        CreateMap<CreateEventDto, Event>()
            .ForMember(d => d.BannerImageUrl, o => o.MapFrom(s => s.BannerUrl))
            .ForMember(d => d.Venue, o => o.MapFrom(s => s.Location))
            .ForMember(d => d.StartsAt, o => o.MapFrom(s => s.StartDate))
            .ForMember(d => d.EndsAt, o => o.MapFrom(s => s.EndDate))
            .ForMember(d => d.MaxStudents, o => o.MapFrom(s => s.MaxAttendees))
            .ForMember(d => d.TargetRole, o => o.MapFrom(s => RoleFor(s.TargetStakeholders)))
            .ForMember(d => d.TargetGradeIdsCsv, o => o.MapFrom(s => FormatIds(s.TargetGradeIds)))
            .ForMember(d => d.TargetSectionIdsCsv, o => o.MapFrom(s => FormatIds(s.TargetSectionIds)))
            .ForMember(d => d.IsPublished, o => o.Ignore())
            .ForMember(d => d.IsCancelled, o => o.Ignore())
            .ForMember(d => d.Registrations, o => o.Ignore())
            .ForMember(d => d.Id, o => o.Ignore());

        CreateMap<UpdateEventDto, Event>()
            .IncludeBase<CreateEventDto, Event>()
            .ForMember(d => d.IsPublished, o => o.MapFrom((s, d) => s.IsActive ?? d.IsPublished));

        CreateMap<EventRegistration, EventRegistrationDto>()
            .ForMember(d => d.RegistrationDate, o => o.MapFrom(s => s.RegisteredAt));
    }

    private static string StakeholdersFor(string role) => role switch
    {
        "Student" => "Students",
        "Teacher" => "Teachers",
        _ => "All"
    };

    private static string RoleFor(string stakeholders) => stakeholders switch
    {
        "Students" => "Student",
        "Teachers" => "Teacher",
        _ => "All"
    };

    private static List<int> ParseIds(string? csv) => string.IsNullOrWhiteSpace(csv)
        ? []
        : csv.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

    private static string? FormatIds(IEnumerable<int> ids)
    {
        var values = (ids ?? []).Where(id => id > 0).Distinct().ToArray();
        return values.Length == 0 ? null : $"|{string.Join('|', values)}|";
    }
}