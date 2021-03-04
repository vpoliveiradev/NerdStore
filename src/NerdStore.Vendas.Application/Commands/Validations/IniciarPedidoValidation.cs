using FluentValidation;
using System;

namespace NerdStore.Vendas.Application.Commands.Validations
{
    public class IniciarPedidoValidation : AbstractValidator<IniciarPedidoCommand>
    {
        public IniciarPedidoValidation()
        {
            RuleFor(c => c.ClienteId)
                .NotEqual(Guid.Empty)
                .WithMessage("Id do cliente inválido");

            RuleFor(c => c.PedidoId)
                .NotEqual(Guid.Empty)
                .WithMessage("Id do pedido inválido");

            RuleFor(c => c.NomeCartao)
                .NotEmpty()
                .WithMessage("O nome no cartão não foi informado");

            RuleFor(c => c.NumeroCartao)
                .CreditCard()
                .WithMessage("Número de cartão de crédito inválido");

            RuleFor(c => c.ExpiracaoCartao)
                .NotEmpty()
                .WithMessage("Data de expiração não informada");

            RuleFor(c => c.CvvCartao)
                .Length(3, 4)
                .WithMessage("O CVV não foi preenchido corretamente");
        }
    }
}
