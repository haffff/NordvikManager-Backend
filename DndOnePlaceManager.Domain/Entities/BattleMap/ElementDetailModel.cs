using DNDOnePlaceManager.Domain.Entities.BattleMap;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Domain.Entities.BattleMap
{
    public class ElementDetailModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public ElementModel Element { get; set; }
        public Guid ElementId { get; set; }
        public string Key { get; set; }
        // Nullable: an explicit JSON null (e.g. a freehand Path's `fill: null`) is a
        // meaningful, distinct value from the key being absent entirely — fabric.js
        // falls back to its own default (black fill) when the key is missing, so this
        // must be able to round-trip through the DB as a real null, not get coerced
        // into an empty string or dropped.
        public string? Value { get; set; }
        public string Type { get; set; }
    }
}
