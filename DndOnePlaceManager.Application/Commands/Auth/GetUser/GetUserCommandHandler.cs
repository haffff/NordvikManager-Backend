
//using AutoMapper;
//using DndOnePlaceManager.Infrastructure.Interfaces;
//using DNDOnePlaceManager.Domain.Entities.Auth;
//using Microsoft.EntityFrameworkCore;

//namespace DndOnePlaceManager.Application.Commands.Auth
//{
//    internal class GetUserCommandHandler : HandlerBase<GetUserCommand, GetUserResponse>
//    {
//        public GetUserCommandHandler(IDbContext userContext, IMapper mapper) : base(userContext, mapper)
//        {
//        }

//        public async override Task<GetUserResponse> Handle(GetUserCommand request, CancellationToken cancellationToken)
//        {
//            await base.Handle(request, cancellationToken);
//            return ToResponse(await dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken));
//        }

//        private GetUserResponse ToResponse(User? value)
//        {
//            if(value == null)
//            {
//                return new GetUserResponse { };
//            }

//            return new GetUserResponse
//            {
//                Id = value.Id,
//                UserName = value.UserName,
//                IsAdmin = value.IsAdmin,
//                User = value
//            };
//        }
//    }
//}

