using FluentValidation;
using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Validation;

public class CreateEventDtoValidator : AbstractValidator<CreateEventDto>
{
    public CreateEventDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.BannerUrl).MaximumLength(500);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Organizer).MaximumLength(150);
        RuleFor(x => x.StartDate).Must(value => value > DateTime.UtcNow).WithMessage("StartDate must be in the future.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue);
        RuleFor(x => x.RegistrationDeadline).LessThanOrEqualTo(x => x.StartDate).When(x => x.RegistrationDeadline.HasValue);
        RuleFor(x => x.MaxAttendees).GreaterThan(0).When(x => x.MaxAttendees.HasValue);
    }
}