using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RequirePermissionStepData
    {
        [Description("ID of the entity to check permissions on.")]
        public string EntityId { get; set; }

        [Description("Permission to require: Read, Execute, Control, Edit, Remove, All.")]
        public string Permission { get; set; }

        [Description("Variable name to store the boolean result. Leave empty to skip storing.")]
        public string Output { get; set; }

        [Description("If true, stops the action execution when permission is denied.")]
        public bool StopIfDenied { get; set; }
    }
}
