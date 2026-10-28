using NordvikManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.DataTransferObjects
{
    public interface IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public Permission? Permission { get; set; }
    }
}
