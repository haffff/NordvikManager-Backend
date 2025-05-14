using DndOnePlaceManager.Application.DataTransferObjects;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository
{
    public class GetAddonsFromRepositoryCommand : CommandBase<List<AddonDto>>
    {
        public string? RepositoryUrl { get; set; }
    }
}
