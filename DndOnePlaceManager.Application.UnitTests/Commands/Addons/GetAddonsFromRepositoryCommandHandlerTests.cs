using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    public class GetAddonsFromRepositoryCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IAddonRepositoryService> _repository = new();

        private GetAddonsFromRepositoryCommandHandler Handler() => new(Mapper, _repository.Object);

        [Fact]
        public async Task Handle_ValidRepositoryJson_ReturnsAddonList()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync(
                "{\"repository\":[{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\"}]}");

            var result = await Handler().Handle(new GetAddonsFromRepositoryCommand(), CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("dnd5e", result[0].Key);
        }

        [Fact]
        public async Task Handle_EmptyRepositoryArray_ReturnsEmptyList()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync("{\"repository\":[]}");

            var result = await Handler().Handle(new GetAddonsFromRepositoryCommand(), CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_MissingRepositoryKey_ThrowsException()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync("{\"other\":[]}");

            await Assert.ThrowsAsync<Exception>(() => Handler().Handle(new GetAddonsFromRepositoryCommand(), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_MalformedJson_ThrowsJsonReaderException()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync("not json");

            await Assert.ThrowsAsync<JsonReaderException>(() => Handler().Handle(new GetAddonsFromRepositoryCommand(), CancellationToken.None));
        }
    }
}
