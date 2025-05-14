using System.ComponentModel;

namespace DndOnePlaceManager.Application.Extension
{
    public static class EnumExtensions
    {
        public static string GetDescriptionValue(this Enum value)
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            return type.GetField(name) // I prefer to get attributes this way
                .GetCustomAttributes(false)
                .OfType<DescriptionAttribute>()
                .SingleOrDefault().Description;
        }

        public static TEnum? ToEnumUsingDescriptionAttribute<TEnum>(this string value)
        where TEnum : Enum
        {
            return (TEnum?)typeof(TEnum)
                .GetFields()
                .FirstOrDefault(f =>
                f.GetCustomAttributes(false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description?.ToLower() == value.ToLower())?.GetRawConstantValue();
        }
    }
}
