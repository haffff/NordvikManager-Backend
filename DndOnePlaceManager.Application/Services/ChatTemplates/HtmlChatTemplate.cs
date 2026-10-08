using DndOnePlaceManager.Application.Services.Rolls;

namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    /// <summary>
    /// A chat message rendered from sender-supplied HTML — e.g. an imported Roll20
    /// sheet's roll template — shown by the frontend in a script-less sandboxed
    /// iframe. Inline roll numbers are NOT taken from the HTML: it carries
    /// placeholders the frontend fills from <see cref="Rolls"/>, which the server
    /// attaches from its own roll session (see RollsController).
    /// </summary>
    public class HtmlChatTemplate : ChatTemplate
    {
        public override string Type => "Html";

        public string Html { get; set; }

        /// <summary>
        /// Key of a game resource holding this message's stylesheet (e.g. one sheet's
        /// roll-template CSS), fetched once per client and reused for every message
        /// that references it — never inlined per message.
        /// </summary>
        public string CssResourceKey { get; set; }

        public RollResultEntry[] Rolls { get; set; }
    }
}
