using DndOnePlaceManager.Domain.Entities.BattleMap;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Extension
{
    public static class StringEntityExtensions
    {
        public static Type? ToEntityType(this string entityName)
        {
            return entityName.ToLower() switch
            {
                "gamemodel" => typeof(GameModel),
                "mapmodel" => typeof(MapModel),
                "elementmodel" => typeof(ElementModel),
                "propertymodel" => typeof(PropertyModel),
                "layoutmodel" => typeof(LayoutModel),
                "cardmodel" => typeof(CardModel),
                _ => null,
            };
        }

        public static MimeType? ToMimeType(this string fileName)
        {
            return Path.GetExtension(fileName).ToLower() switch
            {
                ".png" => MimeType.PNG,
                ".jpg" => MimeType.JPEG,
                ".jpeg" => MimeType.JPEG,
                ".gif" => MimeType.GIF,
                ".tiff" => MimeType.TIFF,
                ".mp3" => MimeType.MP3,
                ".wav" => MimeType.Wave,
                ".ogg" => MimeType.Ogg,
                ".html" => MimeType.HTML,
                ".css" => MimeType.CSS,
                ".json" => MimeType.JSON,
                ".js" => MimeType.JavaScript,
                ".txt" => MimeType.PlainText,
                _ => MimeType.None,
            };
        }
    }
}
