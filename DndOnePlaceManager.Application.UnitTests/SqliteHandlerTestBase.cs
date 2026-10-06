using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Data.Contexts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace DndOnePlaceManager.Application.UnitTests
{
    /// <summary>
    /// Like <see cref="HandlerTestBase"/>, but over a real in-memory SQLite database with the
    /// real <see cref="PermissionsService"/>. Use it where the query shape matters: InMemory
    /// evaluates everything on the client, so it can't show that a query translates to SQL,
    /// nor how many commands a handler sends (<see cref="Commands"/>).
    /// </summary>
    public abstract class SqliteHandlerTestBase : IDisposable
    {
        protected readonly DndOneContext Db;
        protected readonly IMapper Mapper;
        protected readonly IPermissionService Permissions;
        protected readonly CommandCounter Commands = new();
        protected readonly Guid PlayerId = Guid.NewGuid();
        private readonly SqliteConnection connection;
        private readonly ServiceProvider services;

        protected SqliteHandlerTestBase()
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            Db = new DndOneContext(Options().AddInterceptors(Commands).Options);
            Db.Database.EnsureCreated();

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddAutoMapper(x => x.AddProfile(typeof(AutoMapperProfile)));
            collection.AddSingleton<IDbContext>(Db);
            collection.AddScoped<IPermissionService, PermissionsService>();
            services = collection.BuildServiceProvider();

            Mapper = services.GetRequiredService<IMapper>();
            Permissions = services.GetRequiredService<IPermissionService>();
            PermissionsExtension.ServiceProvider = services;
        }

        private DbContextOptionsBuilder<DndOneContext> Options() => new DbContextOptionsBuilder<DndOneContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

        /// <summary>A second context on the same database, for seeding without tracking anything in <see cref="Db"/>.</summary>
        protected DndOneContext SeedContext() => new(Options().Options);

        public void Dispose()
        {
            Db.Dispose();
            services.Dispose();
            connection.Dispose();
        }

        /// <summary>Counts the SQL commands sent through <see cref="Db"/>, keeping their text.</summary>
        public class CommandCounter : DbCommandInterceptor
        {
            public int Count => Texts.Count;

            /// <summary>The SQL of each command, in order.</summary>
            public List<string> Texts { get; } = new();

            public void Reset() => Texts.Clear();

            public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            {
                Texts.Add(command.CommandText);
                return result;
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Texts.Add(command.CommandText);
                return ValueTask.FromResult(result);
            }

            public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
            {
                Texts.Add(command.CommandText);
                return result;
            }

            public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
            {
                Texts.Add(command.CommandText);
                return ValueTask.FromResult(result);
            }
        }
    }
}
