using AutoMapper;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Layouts.AddLayout;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class AddGameCommandHandler : HandlerBase<AddGameCommand, bool>
    {
        IMediator mediator;
        string? mainRepositoryUrl;

        public AddGameCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator, IConfiguration configuration) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
            mainRepositoryUrl = configuration["AddonsConfiguration:MainRepository"];
        }

        public async override Task<bool> Handle(AddGameCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (request.PasswordRequired && string.IsNullOrWhiteSpace(request.Password))
            {
                return false;
            }

            var random = new Random();

            var red = random.Next(0, 255 / 10) * 10;
            var green = random.Next(0, 255 / 10) * 10;
            var blue = random.Next(0, 255 / 10) * 10;

            PlayerModel player = new PlayerModel() { Name = "Game Master", User = request.User?.Id, Color = $"rgba({red},{green},{blue},1)", Image = string.Empty };
            PlayerModel system = new PlayerModel() { Name = "System", System = true, User = null, Color = $"rgba({red},{green},{blue},1)", Image = string.Empty };

            GameModel game = new GameModel()
            {
                Name = request.Name,
                Password = request.PasswordRequired ? request.Password : null,
                Players = new List<PlayerModel>() { player, system },
                Maps = new List<MapModel>()
            };

            dbContext.Add(game);

            dbContext.SaveChanges();

            game.SystemPlayerId = system.Id;
            game.MasterId = player.Id;

            dbContext.SaveChanges();

            game.SetPermissions(player.Id, Domain.Enums.Permission.All);
            game.SetPermissions(system.Id, Domain.Enums.Permission.All);

            GetPlayerCommand getPlayerCommand = new GetPlayerCommand()
            {
                User = request.User,
                GameID = game.Id,
            };

            var playerDTO = (await mediator.Send(getPlayerCommand)).Player;

            bool hasImage = request.Image != null;

            if (hasImage)
            {
                //get mime type from base64 data prefix 
                var mimeType = request.Image.Split("base64,")[0].Split("data:")[1].Split(";")[0];

                //remove base64 data prefix if exists
                if (request.Image.Contains("base64,"))
                {
                    request.Image = request.Image.Split("base64,")[1];
                }

                AddResourceCommand addResourceCommand = new AddResourceCommand()
                {
                    Data = request.Image,
                    GameID = game.Id,
                    Key = "game_image",
                    Player = playerDTO,
                    Name = "Game Image",
                    MimeType = mimeType,
                };

                var (response, id) = await mediator.Send(addResourceCommand);

                AddPropertiesCommand setImageCommand = new AddPropertiesCommand()
                {
                    Player = playerDTO,
                    GameID = game.Id,
                    Properties = [
                    new ()
                        {
                            Name = "image",
                            EntityName = "GameModel",
                            ParentID = game.Id,
                            Value = id?.ToString()
                        }
                ]
                };

                await mediator.Send(setImageCommand);

            }

            AddPropertiesCommand propertiesCommand = new AddPropertiesCommand()
            {
                Player = playerDTO,
                GameID = game.Id,
                Properties = [
                    new ()
                    {
                        Name = "shortDescription",
                        EntityName = "GameModel",
                        ParentID = game.Id,
                        Value = request.Summary
                    },
                    new ()
                    {
                        Name = "longDescription",
                        EntityName = "GameModel",
                        ParentID = game.Id,
                        Value = request.Description
                    },
                    new ()
                    {
                        Name = "allowPlayerUseLayouts",
                        EntityName = "GameModel",
                        ParentID = game.Id,
                        Value = request.AllowPlayersToUseLocalLayouts.ToString()
                    }
                ]
            };

            await mediator.Send(propertiesCommand);

            AddMapCommand addMapCommand = new AddMapCommand()
            {
                GameID = game.Id,
                Dto = new MapDTO()
                {
                    Name = @"Default",
                    Width = 1200,
                    Height = 720,
                    GridVisible = true,
                    GridSize = 50
                },
                Player = playerDTO,
            };

            var (result, mapId) = await mediator.Send(addMapCommand);


            AddBattleMapCommand addBattleMapCommand = new AddBattleMapCommand()
            {
                GameID = game.Id,
                Dto = new BattleMapDto()
                {
                    GameId = game.Id,
                    Name = "Default",
                    MapId = mapId,
                },
                Player = playerDTO,
            };

            var (bmresult, battlemapId) = await mediator.Send(addBattleMapCommand);

            AddLayoutCommand addLayoutCommand = new AddLayoutCommand()
            {
                Player = playerDTO,
                GameID = game.Id,
                Dto = new LayoutDTO()
                {
                    Default = true,
                    Name = "Default",
                    Value = "{\"id\":1,\"floating\":false,\"rect\":{\"x\":0,\"y\":0,\"w\":0,\"h\":0},\"contentList\":[],\"currentTabIndex\":0,\"splitPanels\":[{\"id\":7,\"floating\":false,\"rect\":{\"x\":0,\"y\":0,\"w\":1000,\"h\":1200},\"contentList\":[],\"currentTabIndex\":0,\"splitPanels\":[{\"id\":6,\"floating\":false,\"rect\":{\"x\":-156,\"y\":642,\"w\":300,\"h\":800},\"contentList\":[7],\"currentTabIndex\":0,\"splitPanels\":[],\"splitMode\":0,\"splitSize\":0.5,\"preferredWidth\":300,\"preferredHeight\":800,\"ephemeral\":false,\"isHeaderHidden\":true,\"isLocked\":false},{\"id\":8,\"floating\":false,\"rect\":{\"x\":0,\"y\":0,\"w\":0,\"h\":0},\"contentList\":[3],\"currentTabIndex\":0,\"splitPanels\":[],\"splitMode\":0,\"splitSize\":0.5,\"preferredWidth\":300,\"preferredHeight\":250,\"ephemeral\":false,\"isHeaderHidden\":false,\"isLocked\":false}],\"splitMode\":0,\"splitSize\":0.05,\"preferredWidth\":1000,\"preferredHeight\":1200,\"ephemeral\":false},{\"id\":5,\"floating\":false,\"rect\":{\"x\":0,\"y\":0,\"w\":0,\"h\":0},\"contentList\":[6],\"currentTabIndex\":0,\"splitPanels\":[],\"splitMode\":0,\"splitSize\":0.5,\"preferredWidth\":300,\"preferredHeight\":250,\"ephemeral\":false}],\"splitMode\":0,\"splitSize\":0.75,\"preferredWidth\":300,\"preferredHeight\":250,\"ephemeral\":false,\"_contents\":[{\"contentId\":7,\"type\":\"ToolsPanel\",\"props\":{}},{\"contentId\":3,\"type\":\"Battlemap\",\"syncId\":\""+battlemapId+"\",\"mapId\":\""+mapId+ "\",\"props\":{\"syncId\":\""+battlemapId+"\",\"withID\":\""+battlemapId+"\"}},{\"contentId\":6,\"type\":\"ChatPanel\",\"props\":{}}]}"
                }
            };

            await mediator.Send(addLayoutCommand);

            if(request.AddonsSelected == null || mainRepositoryUrl == null)
            {
                return true;
            }

            foreach (var addon in request.AddonsSelected)
            {
                InstallAddonCommand installAddonCommand = new InstallAddonCommand()
                {
                    AddonSourceKey = addon,
                    GameID = game.Id,
                    Player = playerDTO,
                    AutoInstallDeps = true,
                };

                await mediator.Send(installAddonCommand);
            }

            return true;
        }
    }
}

