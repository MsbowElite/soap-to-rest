using FluentValidation;
using CsharpRest.Application.Models;

namespace CsharpRest.Application.Validators
{
    public class BiometriaRequestValidator : AbstractValidator<BiometriaRequest>
    {
        public BiometriaRequestValidator()
        {
            RuleFor(x => x.Cpf)
                .NotEmpty().WithMessage("CPF é obrigatório.")
                .Must(CpfValidator.IsValid).WithMessage("CPF inválido.");
        }
    }
}
