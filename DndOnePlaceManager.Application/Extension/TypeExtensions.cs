using DndOnePlaceManager.Domain.Entities.BattleMap;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Extension
{
    public static class TypeExtensions
    {
        private static Dictionary<Type, Type> ModelDtoType = new Dictionary<Type, Type> {
            { typeof(GameModel), typeof(GameItemDTO) },
            { typeof(MapModel), typeof(MapDTO) },
            { typeof(ElementModel), typeof(ElementDTO) },
            { typeof(PropertyModel), typeof(PropertyDTO) },
            { typeof(LayoutModel), typeof(LayoutDTO) },
            { typeof(CardModel), typeof(CardDto) } 
        };
        public static Type GetDTOType(this Type type)
        {
            if(ModelDtoType.ContainsKey(type))
            {
                return ModelDtoType[type];
            }
            return null;
        }
    }
}
