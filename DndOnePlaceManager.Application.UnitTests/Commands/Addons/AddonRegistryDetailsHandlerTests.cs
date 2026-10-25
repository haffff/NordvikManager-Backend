using DndOnePlaceManager.Application.Commands.Addons.GetAddonCategories;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonReadme;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    // The registry's categories and an addon's readme, for the Addons panel.
    public class AddonRegistryDetailsHandlerTests
    {
        private readonly Mock<IAddonRepositoryService> _repository = new();

        [Fact]
        public async Task Categories_AreTheRegistrysOwn()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync(
                "{\"categories\":[{\"key\":\"tools\",\"name\":\"Tools\",\"description\":\"Helpers\"}],\"repository\":[]}");

            var result = await new GetAddonCategoriesCommandHandler(_repository.Object).Handle(new GetAddonCategoriesCommand(), CancellationToken.None);

            var tools = Assert.Single(result);
            Assert.Equal(("tools", "Tools", "Helpers"), (tools.Key, tools.Name, tools.Description));
        }

        [Fact]
        public async Task Categories_NoneListed_IsEmpty()
        {
            _repository.Setup(r => r.GetRepository()).ReturnsAsync("{\"repository\":[]}");

            Assert.Empty(await new GetAddonCategoriesCommandHandler(_repository.Object).Handle(new GetAddonCategoriesCommand(), CancellationToken.None));
        }

        [Fact]
        public async Task Readme_ComesFromTheService_ByKey()
        {
            _repository.Setup(r => r.GetReadmeByKey("poll"))
                .ReturnsAsync(new AddonReadmeResult("https://raw.githubusercontent.com/h/X/t/README.md", "# Poll"));

            var result = await new GetAddonReadmeCommandHandler(_repository.Object)
                .Handle(new GetAddonReadmeCommand { Key = "poll" }, CancellationToken.None);

            Assert.Equal(("poll", "https://raw.githubusercontent.com/h/X/t/README.md", "# Poll"), (result!.Key, result.Url, result.Markdown));
        }

        [Fact]
        public async Task Readme_None_IsNull()
        {
            _repository.Setup(r => r.GetReadmeByKey("dnd5e")).ReturnsAsync((AddonReadmeResult?)null);

            Assert.Null(await new GetAddonReadmeCommandHandler(_repository.Object)
                .Handle(new GetAddonReadmeCommand { Key = "dnd5e" }, CancellationToken.None));
        }
    }
}
