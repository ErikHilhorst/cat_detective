namespace CatDetective.Entities
{
    public enum ClueCategory { Who, What, Why, WhereWhen }

    public sealed class Clue
    {
        public string       Id       { get; }
        public ClueCategory Category { get; }
        public string       Name     { get; }
        public string       Context  { get; }

        public Clue(string id, ClueCategory category, string name, string context)
        {
            Id       = id;
            Category = category;
            Name     = name;
            Context  = context;
        }
    }
}
