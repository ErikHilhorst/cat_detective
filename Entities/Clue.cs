namespace CatDetective.Entities
{
    public enum ClueCategory { Who, What, Why, WhereWhen }

    public sealed class Clue
    {
        public string       Id          { get; }
        public ClueCategory Category    { get; }
        public string       DisplayText { get; }

        public Clue(string id, ClueCategory category, string displayText)
        {
            Id          = id;
            Category    = category;
            DisplayText = displayText;
        }
    }
}
