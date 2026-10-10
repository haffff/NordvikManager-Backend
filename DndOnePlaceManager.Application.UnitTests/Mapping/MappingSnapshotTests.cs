using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Chat;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DndOnePlaceManager.Application.UnitTests.Mapping
{
    // The mapping specification: every DTO <-> model pair, mapped from a fully filled
    // fixture, from a fresh instance (nulls and defaults) and from a fixture with empty
    // collections, compared with the golden files in Snapshots/. The golden files were
    // written from the AutoMapper profile this mapper replaced, so they pin its behaviour.
    // Set UPDATE_MAPPING_SNAPSHOTS=1 to rewrite them (only when a mapping changes on purpose).
    public class MappingSnapshotTests
    {
        public static readonly (Type Source, Type Target)[] Pairs =
        {
            (typeof(PlayerDTO), typeof(PlayerModel)), (typeof(PlayerModel), typeof(PlayerDTO)),
            (typeof(MapDTO), typeof(MapModel)), (typeof(MapModel), typeof(MapDTO)),
            (typeof(ElementDTO), typeof(ElementModel)), (typeof(ElementModel), typeof(ElementDTO)),
            (typeof(PropertyDTO), typeof(PropertyModel)), (typeof(PropertyModel), typeof(PropertyDTO)),
            (typeof(LayoutDTO), typeof(LayoutModel)), (typeof(LayoutModel), typeof(LayoutDTO)),
            (typeof(GameModel), typeof(GameItemDTO)),
            (typeof(MessageDTO), typeof(MessageModel)), (typeof(MessageModel), typeof(MessageDTO)),
            (typeof(BattleMapDto), typeof(BattleMapModel)), (typeof(BattleMapModel), typeof(BattleMapDto)),
            (typeof(CardModel), typeof(CardDto)), (typeof(CardDto), typeof(CardModel)),
            (typeof(ActionModel), typeof(ActionDto)), (typeof(ActionDto), typeof(ActionModel)),
            (typeof(AddonModel), typeof(AddonDto)), (typeof(AddonDto), typeof(AddonModel)),
            (typeof(ResourceModel), typeof(ResourceDTO)), (typeof(ResourceDTO), typeof(ResourceModel)),
            (typeof(PlaylistModel), typeof(PlaylistDTO)),
            (typeof(TreeEntryDto), typeof(TreeEntryModel)), (typeof(TreeEntryModel), typeof(TreeEntryDto)),
        };

        public static IEnumerable<object[]> PairNames() => Pairs.Select(p => new object[] { Name(p.Source, p.Target) });

        private static string Name(Type source, Type target) => $"{source.Name}To{target.Name}";

        private readonly IMapper mapper = new AppMapper();

        // Through Map<T>(object), the only call the app makes.
        private object? Map(object? source, Type target) =>
            GetType().GetMethod(nameof(MapAs), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(target).Invoke(this, new[] { source });

        private T MapAs<T>(object? source) => mapper.Map<T>(source);

        [Theory]
        [MemberData(nameof(PairNames))]
        public void Map_MatchesSnapshot_ForEveryPair(string pair)
        {
            var (source, target) = Pairs.Single(p => Name(p.Source, p.Target) == pair);

            var results = new SortedDictionary<string, object?>
            {
                ["filled"] = Capture(() => Map(Fixtures.Filled(source), target)),
                ["fresh"] = Capture(() => Map(Activator.CreateInstance(source)!, target)),
                ["emptyCollections"] = Capture(() => Map(Fixtures.Filled(source, emptyCollections: true), target)),
            };
            AssertSnapshot(pair, results);
        }

        [Fact]
        public void Map_Throws_WhenThereIsNoMappingForThePair()
        {
            var error = Assert.Throws<ArgumentException>(() => mapper.Map<GameModel>(new PlayerDTO()));
            Assert.Contains(nameof(GameModel), error.Message);
        }

        // Lists, as the list handlers map them (Map<List<ActionDto>>, Map<IEnumerable<TResponse>>).
        [Fact]
        public void Map_MatchesSnapshot_ForCollections()
        {
            var results = new SortedDictionary<string, object?>
            {
                ["listOfActions"] = Capture(() => Map(new List<ActionModel> { (ActionModel)Fixtures.Filled(typeof(ActionModel)), new() }, typeof(List<ActionDto>))),
                ["enumerableOfBattleMaps"] = Capture(() => Map(new List<BattleMapModel> { (BattleMapModel)Fixtures.Filled(typeof(BattleMapModel)) }, typeof(IEnumerable<BattleMapDto>))),
                ["emptyList"] = Capture(() => Map(new List<MessageModel>(), typeof(List<MessageDTO>))),
                ["nullToList"] = Capture(() => Map(null, typeof(List<TreeEntryDto>))),
                ["nullToObject"] = Capture(() => Map(null, typeof(ElementDTO))),
            };
            AssertSnapshot("Collections", results);
        }

        private static void AssertSnapshot(string name, SortedDictionary<string, object?> results)
        {
            // Newlines inside values (Newtonsoft's indented JSON) differ per platform.
            var actual = JsonConvert.SerializeObject(results, Serializer).Replace("\\r\\n", "\\n");

            var file = Path.Combine(SnapshotDir(), name + ".json");
            if (Environment.GetEnvironmentVariable("UPDATE_MAPPING_SNAPSHOTS") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, actual + "\n");
            }

            Assert.True(File.Exists(file), $"No snapshot {file}; run with UPDATE_MAPPING_SNAPSHOTS=1 to create it.");
            Assert.Equal(File.ReadAllText(file).TrimEnd().ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
        }

        // A mapping that throws is part of the behaviour too (e.g. an element without Object).
        private static object? Capture(Func<object?> map)
        {
            try { return map(); }
            catch (Exception) { return "<throws>"; }
        }

        private static string SnapshotDir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Snapshots");

        private static readonly JsonSerializerSettings Serializer = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new SortedContractResolver(),
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        // Every property by its C# name: the DTOs' [JsonProperty]/[JsonIgnore] must not hide one.
        private class SortedContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                property.Ignored = false;
                property.PropertyName = property.UnderlyingName;
                return property;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) =>
                base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
        }
    }

    // Deterministic fixtures: every writable property set, nested objects two levels deep (list items included),
    // one item per collection. Fields the mappings parse get valid content.
    internal static class Fixtures
    {
        private const int MaxDepth = 3;

        // An element's fabric.js object, and the details that parse from it (every detail type).
        public const string ElementObject = "{\"type\":\"rect\",\"left\":12,\"visible\":true,\"tokenData\":{\"cardId\":\"c1\"},\"path\":[1,2],\"fill\":null}";

        public static object Filled(Type type, bool emptyCollections = false)
        {
            var counter = 0;
            return Create(type, 0, emptyCollections, ref counter)!;
        }

        private static object? Create(Type type, int depth, bool emptyCollections, ref int counter)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            counter++;

            if (underlying == typeof(string)) return $"text{counter}";
            if (underlying == typeof(Guid)) return new Guid($"00000000-0000-0000-0000-{counter:D12}");
            if (underlying == typeof(bool)) return true;
            if (underlying == typeof(int)) return 100 + counter;
            if (underlying == typeof(double)) return 0.5;
            if (underlying == typeof(DateTime)) return new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            if (underlying == typeof(byte[])) return new byte[] { 1, 2, 3 };
            if (underlying.IsEnum)
            {
                var values = Enum.GetValues(underlying);
                return values.GetValue(Math.Min(1, values.Length - 1));
            }

            if (underlying.IsGenericType && typeof(IEnumerable).IsAssignableFrom(underlying))
            {
                var args = underlying.GetGenericArguments();
                if (args.Length == 2)
                {
                    var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
                    if (!emptyCollections) dictionary.Add(Create(args[0], depth, emptyCollections, ref counter)!, Create(args[1], depth, emptyCollections, ref counter));
                    return dictionary;
                }
                if (depth >= MaxDepth && !IsSimple(args[0])) return null;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args[0]))!;
                if (!emptyCollections) list.Add(Create(args[0], depth + 1, emptyCollections, ref counter));
                return list;
            }

            if (depth >= MaxDepth) return null;
            var instance = Activator.CreateInstance(underlying)!;
            foreach (var property in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
                property.SetValue(instance, Create(property.PropertyType, depth + 1, emptyCollections, ref counter));
            Fix(instance, emptyCollections);
            return instance;
        }

        private static bool IsSimple(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(decimal);
        }

        // The fields a mapping parses must hold what the app stores there.
        private static void Fix(object instance, bool emptyCollections)
        {
            switch (instance)
            {
                case ElementDTO element:
                    element.Object = ElementObject;
                    break;
                case ElementModel element:
                    element.Details = emptyCollections ? new() : new()
                    {
                        Detail(element.Id, "type", "rect", "String"),
                        Detail(element.Id, "left", "12", "Integer"),
                        Detail(element.Id, "visible", "True", "Boolean"),
                        Detail(element.Id, "tokenData", "{\"cardId\":\"c1\"}", "Object"),
                        Detail(element.Id, "path", "[1,2]", "Array"),
                        Detail(element.Id, "fill", null, "Null"),
                    };
                    break;
                case CardModel card:
                    card.AdditionalResources = "[\"00000000-0000-0000-0000-000000000901\"]";
                    break;
                case ResourceDTO resource:
                    resource.MimeType = "audio/mpeg";
                    break;
            }
        }

        private static ElementDetailModel Detail(Guid elementId, string key, string? value, string type) =>
            new() { Id = new Guid($"00000000-0000-0000-0000-{key.Length:D12}"), ElementId = elementId, Key = key, Value = value, Type = type };
    }
}
