using FluentValidation;
using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Validation;

public class UpdateEventDtoValidator : AbstractValidator<UpdateEventDto>
{
    public UpdateEventDtoValidator()
    {
        Include(new CreateEventDtoValidator());
        RuleFor(x => x.IsActive).NotNull();
    }
}