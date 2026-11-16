using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Commands.Resources.DeleteResourceData;
using NordvikManager.Application.Commands.Resources.GetResource;
using NordvikManager.Application.Commands.Resources.GetResourceContent;
using NordvikManager.Application.Commands.Resources.GetResourceRange;
using NordvikManager.Application.Commands.Resources.Upload;
using NordvikManager.Application.Commands.Resources.Link;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Application.Commands.Resources.UpdateResourceData;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
{
    public class MaterialsControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly MaterialsController _controller;

        public MaterialsControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, AnyUser());
        }

        private static MaterialsController CreateController(Mock<IMediator> mediatorMock, User? contextUser = null)
        {
            var lobbyService = new Mock<ILobbyService>().Object;
            var scopeFactory = new Mock<IServiceScopeFactory>().Object;
            var controller = new MaterialsController(mediatorMock.Object, lobbyService, scopeFactory);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static GetPlayerCommandResponse WithPlayer(PlayerDTO? player) =>
            new GetPlayerCommandResponse { Player = player };

        private void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(WithPlayer(player));

        // =========================================================================
        // Without a player in the game, reads are refused
        // =========================================================================

        private static readonly Dictionary<string, Func<MaterialsController, Task<IActionResult>>> PlayerReads = new()
        {
            ["GetResource"] = c => c.GetResource(Guid.NewGuid(), null, Guid.NewGuid()),
            ["GetTemplates"] = c => c.GetTemplates(Guid.NewGuid()),
            ["GetTemplatesFull"] = c => c.GetTemplatesFull(Guid.NewGuid()),
            ["GetCards"] = c => c.GetCards(Guid.NewGuid()),
            ["GetCard"] = c => c.GetCard(Guid.NewGuid(), Guid.NewGuid()),
        };

        public static TheoryData<string> PlayerReadNames() => new(PlayerReads.Keys);

        [Theory]
        [MemberData(nameof(PlayerReadNames))]
        public async Task Read_ReturnsBadRequest_WhenPlayerIsNull(string action)
        {
            SetupPlayer(_mediator, null);

            Assert.IsType<BadRequestResult>(await PlayerReads[action](_controller));
        }

        // =========================================================================
        // What the command answers is what the action answers
        // =========================================================================

        private static TResult Answer<TCommand, TResult>(Mock<IMediator> m, TResult value) where TCommand : IRequest<TResult>
        {
            m.Setup(x => x.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(value);
            return value;
        }

        private static List<CardDto> Cards(Mock<IMediator> m)
        {
            var cards = new List<CardDto>();
            m.Setup(x => x.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, cards));
            return cards;
        }

        private static readonly Dictionary<string, (Func<Mock<IMediator>, object> Setup, Func<MaterialsController, Task<IActionResult>> Call)> Reads = new()
        {
            ["GetResourceMetadata"] = (m => Answer<GetResourceCommand, ResourceDTO>(m, new ResourceDTO { Id = Guid.NewGuid() }), c => c.GetResourceMetadata(Guid.NewGuid(), Guid.NewGuid())),
            ["GetTemplates"] = (Cards, c => c.GetTemplates(Guid.NewGuid())),
            ["GetTemplatesFull"] = (Cards, c => c.GetTemplatesFull(Guid.NewGuid())),
            ["GetCards"] = (Cards, c => c.GetCards(Guid.NewGuid())),
            ["GetCard"] = (m => Answer<GetCardCommand, CardDto>(m, new CardDto { Id = Guid.NewGuid(), Name = "card" }), c => c.GetCard(Guid.NewGuid(), Guid.NewGuid())),
            ["GetResources"] = (m => { var r = new List<ResourceDTO>(); m.Setup(x => x.Send(It.IsAny<GetResourcesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, r)); return r; },
                c => c.GetResources(Guid.NewGuid())),
            ["RemoveResource"] = (m => Answer<RemoveResourceCommand, CommandResponse>(m, CommandResponse.Ok), c => c.RemoveResource(Guid.NewGuid(), Guid.NewGuid())),
        };

        public static TheoryData<string> ReadNames() => new(Reads.Keys);

        [Theory]
        [MemberData(nameof(ReadNames))]
        public async Task Action_ReturnsOk_WithTheCommandsAnswer(string action)
        {
            var expected = Reads[action].Setup(_mediator);

            var ok = Assert.IsType<OkObjectResult>(await Reads[action].Call(_controller));
            Assert.Equal(expected, ok.Value);
        }

        // =========================================================================
        // GetResource
        // =========================================================================

        [Fact]
        public async Task GetResource_ReturnsTheFile_OrBadRequestWithoutData()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((new byte[] { 1, 2, 3 }, MimeType.JPEG));
            Assert.IsType<FileContentResult>(await _controller.GetResource(Guid.NewGuid(), null, Guid.NewGuid()));

            _mediator.Setup(m => m.Send(It.IsAny<GetResourceDataCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(((byte[]?)null, MimeType.JPEG));
            Assert.IsType<BadRequestResult>(await _controller.GetResource(Guid.NewGuid(), null, Guid.NewGuid()));
        }

        // =========================================================================
        // GetResourceWebRTC — what players fetch over the data channel, with a version
        // to cache it under (headers don't cross the tunnel, so it's in the body)
        // =========================================================================

        private static JObject Body(IActionResult result) => JObject.FromObject(Assert.IsType<OkObjectResult>(result).Value!);

        [Fact]
        public async Task GetResourceWebRTC_ReturnsDataWithItsVersion_OrNotFound()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceContentCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ResourceContent(new byte[] { 1, 2 }, MimeType.MP3, "v1", false));

            var body = Body(await _controller.GetResourceWebRTC(Guid.NewGuid(), null, Guid.NewGuid()));

            Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2 }), body["data"]!.Value<string>());
            Assert.Equal("audio/mpeg", body["mimeType"]!.Value<string>());
            Assert.Equal("v1", body["version"]!.Value<string>());

            _mediator.Setup(m => m.Send(It.IsAny<GetResourceContentCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((ResourceContent?)null);
            Assert.IsType<NotFoundResult>(await _controller.GetResourceWebRTC(Guid.NewGuid(), null, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetResourceWebRTC_CachedVersionCurrent_ReturnsNotModifiedWithoutData()
        {
            GetResourceContentCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceContentCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ResourceContent?>, CancellationToken>((c, _) => sent = (GetResourceContentCommand)c)
                     .ReturnsAsync(new ResourceContent(null, MimeType.MP3, "v1", true));

            var body = Body(await _controller.GetResourceWebRTC(Guid.NewGuid(), null, Guid.NewGuid(), thumbnail: true, ifVersion: "v1"));

            Assert.Equal("v1", sent!.IfVersion);
            Assert.True(sent.Thumbnail);
            Assert.True(body["notModified"]!.Value<bool>());
            Assert.Equal("v1", body["version"]!.Value<string>());
            Assert.Null(body["data"]);
        }

        // GetResourceRange — part of a big file (pdf.js asks for the bytes of the pages it shows)
        [Fact]
        public async Task GetResourceRange_ReturnsThePartWithTotalAndVersion()
        {
            GetResourceRangeCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceRangeCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ResourceRange?>, CancellationToken>((c, _) => sent = (GetResourceRangeCommand)c)
                     .ReturnsAsync(new ResourceRange(new byte[] { 5, 6 }, 1024, 9000, MimeType.PDF, "v3", false));

            var body = Body(await _controller.GetResourceRange(Guid.NewGuid(), null, Guid.NewGuid(), start: 1024, length: 2));

            Assert.Equal(1024, sent!.Start);
            Assert.Equal(2, sent.Length);
            Assert.Equal(Convert.ToBase64String(new byte[] { 5, 6 }), body["data"]!.Value<string>());
            Assert.Equal(1024, body["start"]!.Value<long>());
            Assert.Equal(9000, body["total"]!.Value<long>());
            Assert.Equal("application/pdf", body["mimeType"]!.Value<string>());
            Assert.Equal("v3", body["version"]!.Value<string>());
        }

        [Fact]
        public async Task GetResourceRange_CachedVersionCurrent_ReturnsNotModified_AndMissingIsNotFound()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetResourceRangeCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ResourceRange(null, 0, 9000, MimeType.PDF, "v3", true));
            var body = Body(await _controller.GetResourceRange(null, "pdf_src_1.pdf", Guid.NewGuid(), ifVersion: "v3"));
            Assert.True(body["notModified"]!.Value<bool>());
            Assert.Equal(9000, body["total"]!.Value<long>());
            Assert.Null(body["data"]);

            _mediator.Setup(m => m.Send(It.IsAny<GetResourceRangeCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((ResourceRange?)null);
            Assert.IsType<NotFoundResult>(await _controller.GetResourceRange(Guid.NewGuid(), null, Guid.NewGuid()));
        }

        // Uploads in parts — a big file without one huge request, resumable after a drop
        [Fact]
        public async Task UploadStart_ReturnsTheUploadId_OrConflictForATakenKey()
        {
            var uploadId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<StartUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, (Guid?)uploadId));
            var body = Body(await _controller.UploadStart(Guid.NewGuid(), new UploadStartRequest { Name = "book.pdf", Size = 10 }));
            Assert.Equal(uploadId, body["uploadId"]!.Value<Guid>());

            _mediator.Setup(m => m.Send(It.IsAny<StartUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.AlreadyExists, (Guid?)null));
            Assert.IsType<ConflictObjectResult>(await _controller.UploadStart(Guid.NewGuid(), new UploadStartRequest { Name = "x", Key = "taken", Size = 1 }));
        }

        [Fact]
        public async Task UploadPart_DecodesThePart_AndAWrongOffsetSaysWhereToContinue()
        {
            UploadPartCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<UploadPartCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, long)>, CancellationToken>((c, _) => sent = (UploadPartCommand)c)
                     .ReturnsAsync((CommandResponse.Ok, 3L));
            var id = Guid.NewGuid();
            var ok = Body(await _controller.UploadPart(Guid.NewGuid(), new UploadPartRequest { UploadId = id, Offset = 0, Data = Convert.ToBase64String(new byte[] { 1, 2, 3 }) }));
            Assert.Equal(3, ok["received"]!.Value<long>());
            Assert.Equal(new byte[] { 1, 2, 3 }, sent!.Data);
            Assert.Equal(id, sent.UploadId);

            _mediator.Setup(m => m.Send(It.IsAny<UploadPartCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.WrongArguments, 3L));
            var conflict = Assert.IsType<ConflictObjectResult>(await _controller.UploadPart(Guid.NewGuid(), new UploadPartRequest { UploadId = id, Offset = 0, Data = "AQ==" }));
            Assert.Equal(3, JObject.FromObject(conflict.Value!)["received"]!.Value<long>());

            Assert.IsType<BadRequestObjectResult>(await _controller.UploadPart(Guid.NewGuid(), new UploadPartRequest { UploadId = id, Data = "not base64!" }));
        }

        [Fact]
        public async Task UploadStatus_AndFinish()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetUploadStatusCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((long?)5);
            Assert.Equal(5, Body(await _controller.UploadStatus(Guid.NewGuid(), Guid.NewGuid()))["received"]!.Value<long>());
            _mediator.Setup(m => m.Send(It.IsAny<GetUploadStatusCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);
            Assert.IsType<NotFoundResult>(await _controller.UploadStatus(Guid.NewGuid(), Guid.NewGuid()));

            var resourceId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<FinishUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new UploadFinishResult(CommandResponse.Ok, resourceId, true, "book.pdf", "application/pdf", Guid.NewGuid()));
            Assert.Equal(resourceId, Body(await _controller.UploadFinish(Guid.NewGuid(), new UploadFinishRequest { UploadId = Guid.NewGuid() }))["id"]!.Value<Guid>());

            _mediator.Setup(m => m.Send(It.IsAny<FinishUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new UploadFinishResult(CommandResponse.WrongArguments, null, false, "x", null, Guid.NewGuid()));
            Assert.IsType<BadRequestObjectResult>(await _controller.UploadFinish(Guid.NewGuid(), new UploadFinishRequest { UploadId = Guid.NewGuid() }));
        }

        // =========================================================================
        // Writes: Ok when the command succeeds; its exceptions go through (the exception
        // middleware turns them into 403/404/400)
        // =========================================================================

        private static readonly Dictionary<string, (Action<Mock<IMediator>> Setup, Func<MaterialsController, Task<IActionResult>> Call, Type Expected)> Writes = new()
        {
            ["AddResource"] = (m => m.Setup(x => x.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, (Guid?)Guid.NewGuid())),
                c => c.AddResource(Guid.NewGuid(), new AddResourceRequest { Name = "image", MimeType = "image/jpeg", Key = null, Data = "aGVsbG8=" }), typeof(OkObjectResult)),
            ["CreateResource"] = (m => m.Setup(x => x.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, (Guid?)Guid.NewGuid())),
                CreateBgMusic, typeof(OkObjectResult)),
            ["UpdateResourceData"] = (m => m.Setup(x => x.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, (Guid?)Guid.NewGuid())),
                UpdateContent, typeof(OkObjectResult)),
            ["DeleteResourceData"] = (m => m.Setup(x => x.Send(It.IsAny<DeleteResourceDataCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(CommandResponse.Ok),
                DeleteData, typeof(OkResult)),
        };

        private static Task<IActionResult> CreateBgMusic(MaterialsController c) => c.CreateResource(Guid.NewGuid(), new CreateResourceRequest { Key = "bg-music", Content = "aGVsbG8=" });
        private static Task<IActionResult> UpdateContent(MaterialsController c) => c.UpdateResourceData(Guid.NewGuid(), new UpdateResourceDataRequest { Id = Guid.NewGuid(), Content = "aGVsbG8=" });
        private static Task<IActionResult> DeleteData(MaterialsController c) => c.DeleteResourceData(Guid.NewGuid(), null, Guid.NewGuid());

        public static TheoryData<string> WriteNames() => new(Writes.Keys);

        [Theory]
        [MemberData(nameof(WriteNames))]
        public async Task Write_ReturnsOk_WhenTheCommandSucceeds(string action)
        {
            Writes[action].Setup(_mediator);

            var result = await Writes[action].Call(_controller);

            Assert.IsType(Writes[action].Expected, result);
            if (result is OkObjectResult ok) Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task CreateResource_ReturnsConflict_WhenKeyAlreadyExists()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.AlreadyExists, (Guid?)null));

            Assert.IsType<ConflictObjectResult>(await CreateBgMusic(_controller));
        }

        public static TheoryData<string, string> Refusals() => new()
        {
            { "CreateResource", "player missing in the database" },
            { "UpdateResourceData", "resource missing" },
            { "UpdateResourceData", "not the owner" },
            { "UpdateResourceData", "content not base64" },
            { "DeleteResourceData", "resource missing" },
            { "DeleteResourceData", "not the owner" },
        };

        private static Exception RefusalFor(string why) => why switch
        {
            "player missing in the database" => new PermissionException(Permission.Read),
            "resource missing" => new ResourceNotFoundException("Resource", Guid.NewGuid()),
            "not the owner" => new PermissionException(Permission.Edit),
            _ => new WrongArgumentsException(nameof(UpdateResourceDataRequest.Content)),
        };

        [Theory]
        [MemberData(nameof(Refusals))]
        public async Task Write_LetsTheCommandsRefusalThrough(string action, string why)
        {
            var refusal = RefusalFor(why);
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(refusal);
            _mediator.Setup(m => m.Send(It.IsAny<UpdateResourceDataCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(refusal);
            _mediator.Setup(m => m.Send(It.IsAny<DeleteResourceDataCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(refusal);

            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => Writes[action].Call(_controller));
            Assert.Same(refusal, thrown);
        }

        // =========================================================================
        // The host's disk: local admin only
        // =========================================================================

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BrowseLocalDirectory_PassesWhetherTheUserIsLocalAdmin(bool isLocalAdmin)
        {
            BrowseLocalDirectoryCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<BrowseLocalDirectoryCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<IReadOnlyList<LocalDirectoryEntry>>, CancellationToken>((c, _) => sent = (BrowseLocalDirectoryCommand)c)
                     .ReturnsAsync(new List<LocalDirectoryEntry>());
            var controller = CreateController(_mediator, new User { Id = "user-id", UserName = "user", IsLocalAdmin = isLocalAdmin });

            await controller.BrowseLocalDirectory(Guid.NewGuid(), null);

            Assert.Equal(isLocalAdmin, sent!.IsLocalAdmin);
        }

        [Fact]
        public async Task LinkDirectory_NotLocalAdmin_IsForbiddenBeforeAnythingStarts()
        {
            var result = await _controller.LinkDirectory(Guid.NewGuid(), new LinkDirectoryRequest { LocalDirectoryPath = "C:/" });

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetResources_PassesWhetherTheUserIsLocalAdmin()
        {
            GetResourcesCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<GetResourcesCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, List<ResourceDTO>)>, CancellationToken>((c, _) => sent = (GetResourcesCommand)c)
                     .ReturnsAsync((CommandResponse.Ok, new List<ResourceDTO>()));
            var controller = CreateController(_mediator, new User { Id = "user-id", UserName = "user", IsLocalAdmin = true });

            await controller.GetResources(Guid.NewGuid());

            Assert.True(sent!.IsLocalAdmin);
        }

        // The client builds a token for "place this card's token" here too, so the client and
        // the server's SpawnToken place tokens the same way.
        [Fact]
        public async Task BuildToken_BuildsItAsThePlayer()
        {
            var player = SomePlayer();
            SetupPlayer(_mediator, player);
            var built = new NordvikManager.Application.Commands.Actions.BuildToken.BuiltToken(new JObject { ["name"] = "Goblin" }, 2);
            NordvikManager.Application.Commands.Actions.BuildToken.BuildTokenCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<NordvikManager.Application.Commands.Actions.BuildToken.BuildTokenCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<NordvikManager.Application.Commands.Actions.BuildToken.BuiltToken>, CancellationToken>((c, _) => sent = (NordvikManager.Application.Commands.Actions.BuildToken.BuildTokenCommand)c)
                .ReturnsAsync(built);
            var mapId = Guid.NewGuid();
            var cardId = Guid.NewGuid();

            var result = await _controller.BuildToken(Guid.NewGuid(), new NordvikManager.Models.BuildTokenRequest { MapId = mapId, CardId = cardId, X = 10, Y = 20 });

            var body = Body(result);
            Assert.Equal("Goblin", (string)JObject.Parse((string)body["object"]!)["name"]);
            Assert.Equal(2, (int)body["size"]!);
            Assert.Equal((mapId, cardId, 10.0, 20.0, player.Id), (sent.MapId, sent.CardId, sent.X, sent.Y, sent.Player.Id));
        }
    }
}
