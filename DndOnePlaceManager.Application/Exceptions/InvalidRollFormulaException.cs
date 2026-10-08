namespace DndOnePlaceManager.Application.Exceptions
{
    /// <summary>A formula in a roll batch the dice engine couldn't evaluate.</summary>
    public class InvalidRollFormulaException : Exception
    {
        public string Key { get; }
        public string Formula { get; }

        public InvalidRollFormulaException(string key, string formula, Exception inner)
            : base($"Invalid roll formula for '{key}': {formula}", inner)
        {
            Key = key;
            Formula = formula;
        }
    }
}
