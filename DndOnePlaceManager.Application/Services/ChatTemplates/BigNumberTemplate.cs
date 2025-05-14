namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class BigNumberTemplate : ChatTemplate
    {
        public override string Type => "BigNumber";
        public string Number { get; set; }
    }
}
