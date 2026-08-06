using System.Diagnostics.CodeAnalysis;
using DndOnePlaceManager.Application.Exceptions;

namespace DndOnePlaceManager.Application.Guards
{
    /// <summary>
    /// Single call-site idiom for the guard clauses used across command handlers.
    /// For plain argument-null checks, use ArgumentNullException.ThrowIfNull(x) directly.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Throws <see cref="ResourceNotFoundException"/> if <paramref name="value"/> is null.
        /// Requires an identifier so the message identifies what was looked up, not just the
        /// local variable name.
        /// </summary>
        public static void NotFound<T>([NotNull] T? value, string resourceName, object identifier) where T : class
        {
            if (value == null)
            {
                throw new ResourceNotFoundException(resourceName, identifier);
            }
        }

        /// <summary>
        /// Throws <see cref="WrongArgumentsException"/> if <paramref name="condition"/> is false.
        /// </summary>
        public static void Argument(bool condition, string fieldName)
        {
            if (!condition)
            {
                throw new WrongArgumentsException(fieldName);
            }
        }

        /// <summary>
        /// Throws <see cref="WrongArgumentsException"/> if <paramref name="condition"/> is false.
        /// </summary>
        public static void Argument(bool condition, params string[] fieldNames)
        {
            if (!condition)
            {
                throw new WrongArgumentsException(fieldNames);
            }
        }
    }
}
