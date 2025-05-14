using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DndOnePlaceManager.Infrastructure.Data.Extensions
{
    public static class DbContextExtensions
    {
        public static IQueryable Set(this IDbContext context, Type T)
        {
            // Get the generic type definition
            MethodInfo method = typeof(DbContext).GetMethod(nameof(DbContext.Set), BindingFlags.Public | BindingFlags.Instance);

            // Build a method with the specific type argument you're interested in
            method = method.MakeGenericMethod(T);

            return method.Invoke(context, null) as IQueryable;
        }
    }
}
