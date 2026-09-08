using System.ComponentModel;

namespace DndOnePlaceManager.Application.Extension
{
    public static class EnumExtensions
    {
        public static string GetDescriptionValue(this Enum value)
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            return type.GetField(name)
                .GetCustomAttributes(false)
                .OfType<DescriptionAttribute>()
                .SingleOrDefault()?.Description;
        }

        // The `struct` constraint is load-bearing, not decorative — with only
        // `where TEnum : Enum` (every enum type technically satisfies this, but
        // the compiler can't statically prove TEnum is a value type from that
        // constraint alone), `TEnum?` below silently compiles down to plain
        // TEnum instead of Nullable<TEnum> — confirmed by inspecting the
        // compiled signature, which showed a non-nullable `MimeType` return
        // type despite the `TEnum?` written here. That means the "no match"
        // path — `(TEnum?)null!.GetRawConstantValue()` when FirstOrDefault
        // finds nothing — was unboxing a null object into a non-nullable
        // value type, which throws at runtime instead of yielding a real
        // null. This was a latent, pre-existing bug for ANY unrecognized
        // input, not something newly introduced — it just never surfaced
        // because nothing previously exercised the "value matches no known
        // Description" path with the resulting null actually reaching a
        // `?? fallback` for the first time until it did (via an addon zip
        // shipping a file with an unrecognized extension).
        public static TEnum? ToEnumUsingDescriptionAttribute<TEnum>(this string value)
        where TEnum : struct, Enum
        {
            // A null/empty value (e.g. an unrecognized file extension resolving to
            // MimeType.None, which has no [Description]) must resolve to "no match"
            // like any other non-matching input — not throw. Every caller already
            // treats a null result as "fall back to a default", so this stays a
            // pure lookup rather than gaining new failure semantics.
            if (string.IsNullOrEmpty(value)) return null;

            var field = typeof(TEnum)
                .GetFields()
                .FirstOrDefault(f =>
                f.GetCustomAttributes(false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description?.ToLower() == value.ToLower());
            if (field == null) return null;

            // FieldInfo.GetRawConstantValue() returns the field's UNDERLYING
            // representation (a boxed Int32 for a standard enum), not a boxed
            // TEnum instance — casting that directly to TEnum? throws
            // InvalidCastException (unboxing a boxed Int32 as Nullable<TEnum>
            // requires an exact type match Int32 doesn't have, unlike the
            // CLR's special-cased leniency for unboxing straight to the
            // non-nullable enum type). Enum.ToObject performs the correct
            // underlying-value → enum-instance conversion first.
            return (TEnum)Enum.ToObject(typeof(TEnum), field.GetRawConstantValue()!);
        }
    }
}
