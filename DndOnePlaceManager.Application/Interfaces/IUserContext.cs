using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Interfaces
{
    public interface IUserContext : IDbContextBase
    {
       DbSet<User> Users { get; }
    }
}
