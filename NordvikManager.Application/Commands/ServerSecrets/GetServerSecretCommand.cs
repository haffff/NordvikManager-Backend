using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Entities;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace NordvikManager.Application.Commands.ServerSecrets
{
    /// <summary>
    /// This server's secret by that name (32 random bytes), made the first time it's asked
    /// for and kept in the database. Never send or log it.
    /// </summary>
    public class GetServerSecretCommand : CommandBase<byte[]>
    {
        public string Name { get; set; } = "";
    }

    internal class GetServerSecretCommandHandler : HandlerBase<GetServerSecretCommand, byte[]>
    {
        public GetServerSecretCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper) { }

        public override async Task<byte[]> Handle(GetServerSecretCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var kept = await Find(request.Name, cancellationToken);
            if (kept != null)
                return kept;

            var secret = new ServerSecretModel { Name = request.Name, Value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
            dbContext.ServerSecrets.Add(secret);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Made by another request meanwhile: theirs is the one.
                dbContext.ServerSecrets.Entry(secret).State = EntityState.Detached;
                return await Find(request.Name, cancellationToken)
                    ?? throw new InvalidOperationException($"The server secret \"{request.Name}\" can't be saved.");
            }
            return Convert.FromBase64String(secret.Value);
        }

        private async Task<byte[]?> Find(string name, CancellationToken cancellationToken)
        {
            var value = await dbContext.ServerSecrets.AsNoTracking()
                .Where(s => s.Name == name)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);
            return value == null ? null : Convert.FromBase64String(value);
        }
    }
}
