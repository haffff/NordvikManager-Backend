using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.DataTransferObjects.Security
{
    public class PermissionDTO
    {
        public long Id { get; set; }
        public long ModelID { get; set; }
        public string ModelType { get; set; }
        public Permission Permission { get; set; }
    }
}
