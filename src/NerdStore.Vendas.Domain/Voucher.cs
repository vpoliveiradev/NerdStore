using FluentValidation;
using FluentValidation.Results;
using NerdStore.Core.DomainObjects;
using System;
using System.Collections.Generic;

namespace NerdStore.Vendas.Domain
{
    public class Voucher : Entity
    {
        public string Codigo { get; private set; }
        public decimal? Percentual { get; private set; }
        public decimal? ValorDesconto { get; private set; }
        public int Quantidade { get; private set; }
        public TipoDescontoVoucher TipoDescontoVoucher { get; private set; }
        public DateTime DataCriacao { get; private set; }
        public DateTime? DataUtilizacao { get; private set; }
        public DateTime DataValidade { get; private set; }
        public bool Ativo { get; private set; }
        public bool Utilizado { get; private set; }

        //EF Relational
        public ICollection<Pedido> Pedidos { get; set; }

        //Retorna se é válido e também as mensagens de erro da regra de negócio do voucher
        //internal para não ter acesso em outro assembly, pois está sendo usado no aggregate root deste contexto, ou seja, está sendo usado no Pedido
        //Reforçando o conceito de aggregate root onde se manipula a classe raiz e não as classes filhas
        internal ValidationResult ValidarSeAplicavel()
        {
            return new VoucherAplicavelValidation().Validate(this);
        }
    }

    public class VoucherAplicavelValidation : AbstractValidator<Voucher>
    {
        public VoucherAplicavelValidation()
        {
            RuleFor(v => v.DataValidade)
                .Must(DataVencimentoSuperiorAtual)
                .WithMessage("Este voucher está expirado.");

            RuleFor(v => v.Ativo)
                .Equal(true)
                .WithMessage("Este voucher não é mais válido.");

            RuleFor(v => v.Utilizado)
                .Equal(false)
                .WithMessage("Este voucher já foi utilizado.");

            RuleFor(v => v.Quantidade)
                .GreaterThan(0)
                .WithMessage("Este voucher não está mais disponível.");
        }

        protected static bool DataVencimentoSuperiorAtual(DateTime dataValidade)
        {
            return dataValidade >= DateTime.Now;
        }
    }
}
