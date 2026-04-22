using System.ComponentModel;
using DNDOnePlaceManager.Models;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class QueryDataStepData
    {
        [UIType("textarea")]
        [Description("Multi-line variable assignments. Each line: variableName=expression.\n" +
                     "Expressions support %q:% and %qn:% query syntax (already resolved before this step runs).\n" +
                     "Example:\n" +
                     "  gameName=%q:{gameId}.name%\n" +
                     "  playerColor=%q:{playerId}.color%")]
        public string Assignments { get; set; }
    }
}
