using FluentValidation;
using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Validation;

public class RegisterEventDtoValidator : AbstractValidator<RegisterEventDto>
{
    public RegisterEventDtoValidator()
    {
        RuleFor(x => x.StudentId).GreaterThan(0);
    }
}