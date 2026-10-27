using NordvikManager.Domain.Enums;

namespace NordvikManager.Domain.Entities.Interfaces
{
    public interface IEntity
    {
        public Guid Id { get; }
    }
}
