using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ValueStepData
    {
        [Description("A literal value or variable reference to pass forward in the action chain.")]
        public string Value { get; set; }
    }
}
