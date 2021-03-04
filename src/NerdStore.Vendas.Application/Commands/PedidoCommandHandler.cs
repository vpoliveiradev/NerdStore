using MediatR;
using NerdStore.Vendas.Domain;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NerdStore.Core.Communication.Mediator;
using NerdStore.Core.Messages.CommonMessages.Notifications;
using NerdStore.Vendas.Application.Events;
using NerdStore.Core.Messages;
using NerdStore.Core.DomainObjects.DTO;
using System.Collections.Generic;
using NerdStore.Core.Extensions;
using NerdStore.Core.Messages.CommonMessages.IntegrationEvents;

namespace NerdStore.Vendas.Application.Commands
{
    public class PedidoCommandHandler :
        IRequestHandler<AdicionarItemPedidoCommand, bool>,
        IRequestHandler<AtualizarItemPedidoCommand, bool>,
        IRequestHandler<RemoverItemPedidoCommand, bool>,
        IRequestHandler<AplicarVoucherPedidoCommand, bool>,
        IRequestHandler<IniciarPedidoCommand, bool>,
        IRequestHandler<FinalizarPedidoCommand, bool>,
        IRequestHandler<CancelarProcessamentoPedidoEstornarEstoqueCommand, bool>,
        IRequestHandler<CancelarProcessamentoPedidoCommand, bool>
    
    {
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IMediatorHandler _mediatorHandler;

        public PedidoCommandHandler(IPedidoRepository pedidoRepository,
                                    IMediatorHandler mediatorHandler)
        {
            _pedidoRepository = pedidoRepository;
            _mediatorHandler = mediatorHandler;
        }

        public async Task<bool> Handle(AdicionarItemPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            if (!ValidarComando(mensagem)) return false;

            var pedido = await _pedidoRepository.ObterPedidoRascunhoPorClienteId(mensagem.ClienteId);
            var pedidoItem = new PedidoItem(mensagem.ProdutoId, mensagem.Nome, mensagem.Quantidade, mensagem.ValorUnitario);

            if (pedido == null)
            {
                pedido = Pedido.PedidoFactory.NovoPedidoRascunho(mensagem.ClienteId);
                pedido.AdicionarItem(pedidoItem);

                _pedidoRepository.Adicionar(pedido);
                pedido.AdicionarEvento(new PedidoRascunhoIniciadoEvent(mensagem.ClienteId, mensagem.ProdutoId));
            }
            else
            {
                var pedidoItemExistente = pedido.PedidoItemExistente(pedidoItem);
                pedido.AdicionarItem(pedidoItem);

                if (pedidoItemExistente)
                {
                    _pedidoRepository.AtualizarItem(pedido.PedidoItems.FirstOrDefault(p => p.ProdutoId == pedidoItem.ProdutoId));
                }
                else
                {
                    _pedidoRepository.AdicionarItem(pedidoItem);
                }
            }

            pedido.AdicionarEvento(new PedidoItemAdicionadoEvent(pedido.ClienteId, pedido.Id, mensagem.ProdutoId, mensagem.Nome, mensagem.ValorUnitario, mensagem.Quantidade));
            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(AtualizarItemPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            if (!ValidarComando(mensagem)) return false;

            var pedido = await _pedidoRepository.ObterPedidoRascunhoPorClienteId(mensagem.ClienteId);

            if (pedido == null)
            {
                //Publico apenas notificações que eu queira que meu cliente veja
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado!"));
                return false;
            }

            var pedidoItem = await _pedidoRepository.ObterItemPorPedido(pedido.Id, mensagem.ProdutoId);

            if (!pedido.PedidoItemExistente(pedidoItem))
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Item do pedido não encontrado!"));
                return false;
            }

            pedido.AtualizarUnidades(pedidoItem, mensagem.Quantidade);
            pedido.AdicionarEvento(new PedidoProdutoAtualizadoEvent(mensagem.ClienteId, pedido.Id, mensagem.ProdutoId, mensagem.Quantidade));

            _pedidoRepository.AtualizarItem(pedidoItem);
            _pedidoRepository.Atualizar(pedido);

            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(RemoverItemPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            if (!ValidarComando(mensagem)) return false;

            var pedido = await _pedidoRepository.ObterPedidoRascunhoPorClienteId(mensagem.ClienteId);

            if (pedido == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado!"));
                return false;
            }

            var pedidoItem = await _pedidoRepository.ObterItemPorPedido(pedido.Id, mensagem.ProdutoId);

            if (pedidoItem != null && !pedido.PedidoItemExistente(pedidoItem))
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Item do pedido não encontrado!"));
                return false;
            }

            pedido.RemoverItem(pedidoItem);
            pedido.AdicionarEvento(new PedidoProdutoRemovidoEvent(mensagem.ClienteId, pedido.Id, mensagem.ProdutoId));

            _pedidoRepository.RemoverItem(pedidoItem);
            _pedidoRepository.Atualizar(pedido);

            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(AplicarVoucherPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            if (!ValidarComando(mensagem)) return false;

            var pedido = await _pedidoRepository.ObterPedidoRascunhoPorClienteId(mensagem.ClienteId);

            if (pedido == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado!"));
                return false;
            }

            var voucher = await _pedidoRepository.ObterVoucherPorCodigo(mensagem.CodigoVoucher);

            if (voucher == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Voucher não encontrado"));
                return false;
            }

            var voucherAplicacaoValidation = pedido.AplicarVoucher(voucher);
            if (!voucherAplicacaoValidation.IsValid)
            {
                foreach (var error in voucherAplicacaoValidation.Errors)
                {
                    await _mediatorHandler.PublicarNotificacao(new DomainNotification(error.ErrorCode, error.ErrorMessage));

                }
                return false;
            }

            pedido.AdicionarEvento(new VoucherAplicadoPedidoEvent(mensagem.ClienteId, pedido.Id, voucher.Id));

            _pedidoRepository.Atualizar(pedido);

            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(IniciarPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            if (!ValidarComando(mensagem)) return false;

            var pedido = await _pedidoRepository.ObterPedidoRascunhoPorClienteId(mensagem.ClienteId);
            pedido.IniciarPedido();

            var listaDeItens = new List<Item>();
            pedido.PedidoItems.ForEach(i => listaDeItens.Add(new Item { Id = i.ProdutoId, Quantidade = i.Quantidade }));
            var listaProdutosPedido = new ListaProdutosPedido { PedidoId = pedido.Id, Itens = listaDeItens };

            pedido.AdicionarEvento(new PedidoIniciadoEvent(pedido.Id, pedido.ClienteId, listaProdutosPedido, pedido.ValorTotal, mensagem.NomeCartao, mensagem.NumeroCartao, mensagem.ExpiracaoCartao, mensagem.CvvCartao));

            _pedidoRepository.Atualizar(pedido);
            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(FinalizarPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            var pedido = await _pedidoRepository.ObterPorId(mensagem.PedidoId);

            if(pedido == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado"));
                return false;
            }

            pedido.FinalizarPedido();

            pedido.AdicionarEvento(new PedidoFinalizadoEvent(mensagem.PedidoId));
            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(CancelarProcessamentoPedidoEstornarEstoqueCommand mensagem, CancellationToken cancellationToken)
        {
            var pedido = await _pedidoRepository.ObterPorId(mensagem.PedidoId);

            if (pedido == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado!"));
                return false;
            }

            var itensList = new List<Item>();
            pedido.PedidoItems.ForEach(i => itensList.Add(new Item { Id = i.ProdutoId, Quantidade = i.Quantidade }));
            var listaProdutosPedido = new ListaProdutosPedido { PedidoId = pedido.Id, Itens = itensList };

            pedido.AdicionarEvento(new PedidoProcessamentoCanceladoEvent(pedido.Id, pedido.ClienteId, listaProdutosPedido));
            pedido.TornarRascunho();

            return await _pedidoRepository.UnitOfWork.Commit();
        }

        public async Task<bool> Handle(CancelarProcessamentoPedidoCommand mensagem, CancellationToken cancellationToken)
        {
            var pedido = await _pedidoRepository.ObterPorId(mensagem.PedidoId);

            if (pedido == null)
            {
                await _mediatorHandler.PublicarNotificacao(new DomainNotification("pedido", "Pedido não encontrado!"));
                return false;
            }

            pedido.TornarRascunho();

            return await _pedidoRepository.UnitOfWork.Commit();
        }

        private bool ValidarComando(Command mensagem)
        {
            if (mensagem.EhValido()) return true;

            foreach (var error in mensagem.ValidationResult.Errors)
            {
                _mediatorHandler.PublicarNotificacao(new DomainNotification(mensagem.MessageType, error.ErrorMessage));
            }

            return false;
        }

    }
}