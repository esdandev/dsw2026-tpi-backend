using FluentValidation;
using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Validators;

public class DoctorGetAllQueryValidator : AbstractValidator<DoctorModel.GetAllQuery>
{
    public DoctorGetAllQueryValidator()
    {
        RuleFor(x => x.PageSize).GreaterThan(0).WithMessage("debe ser mayor a 0");
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(0).WithMessage("no puede ser negativo");
        RuleFor(x => x.Name)
            .Length(1, 100).WithMessage("debe tener entre 1 y 100 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));
    }
}