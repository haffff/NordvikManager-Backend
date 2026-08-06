using DndOnePlaceManager.Application.Commands.Application;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Application
{
    public class GetVersionInfoCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IVersionService> _versionService = new();

        private GetVersionInfoCommandHandler Handler() => new(Db, Mapper, _versionService.Object);

        [Fact]
        public async Task Handle_NewerVersionAvailable_SetsIsUpdateAvailableTrue()
        {
            _versionService.Setup(v => v.GetVersionJSONAsync())
                .ReturnsAsync("{\"currentVersion\":\"1.2.3\",\"changes\":[\"a\",\"b\"]}");

            var result = await Handler().Handle(new GetVersionInfoCommand(), CancellationToken.None);

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("0.0.0", result.Version);
            Assert.Equal("1.2.3", result.CurrentVersion);
        }

        [Fact]
        public async Task Handle_SameVersion_SetsIsUpdateAvailableFalse()
        {
            _versionService.Setup(v => v.GetVersionJSONAsync())
                .ReturnsAsync("{\"currentVersion\":\"0.0.0\"}");

            var result = await Handler().Handle(new GetVersionInfoCommand(), CancellationToken.None);

            Assert.False(result.IsUpdateAvailable);
        }

        [Fact]
        public async Task Handle_ServiceReturnsNullJson_ThrowsException()
        {
            _versionService.Setup(v => v.GetVersionJSONAsync()).ReturnsAsync("null");

            await Assert.ThrowsAsync<Exception>(() => Handler().Handle(new GetVersionInfoCommand(), CancellationToken.None));
        }
    }
}
