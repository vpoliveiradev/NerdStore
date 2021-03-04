using NerdStore.Core.DomainObjects;
using System;

namespace NerdStore.Core.Data
{
    //Para atender um repositório por agregação T = IAggregateRoot
    public interface IRepository<T> : IDisposable where T : IAggregateRoot
    {
        IUnitOfWork UnitOfWork { get; }
    }
}
