using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class RollFormulaRequest
    {
        public string? Key { get; set; }
        public string? Formula { get; set; }
    }

    public class StartRollRequest
    {
        public List<RollFormulaRequest>? Formulas { get; set; }
    }

    public class FinishRollRequest
    {
        public Guid RollId { get; set; }
        public string? Html { get; set; }
        public string? CssResourceKey { get; set; }
        public string? Title { get; set; }
    }
}
