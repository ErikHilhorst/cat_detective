namespace CatDetective.Entities
{
    public enum ClueCategory { Who, What, Why, WhereWhen }

    public sealed class Clue
    {
        public string       Id                   { get; }
        public ClueCategory Category             { get; }
        public string       Name                 { get; }
        public string       Context              { get; }
        /// <summary>Shown in the Inspector Panel when the clue is selected in the Word Bank.</summary>
        public string       InspectorDescription { get; }
        /// <summary>Which room this clue originates from. Empty string means case-global.</summary>
        public string       RoomId               { get; }
        /// <summary>True for clues that feed the final case-level deduction rather than a room sub-puzzle.</summary>
        public bool         IsMacroClue          { get; }

        public Clue(string id, ClueCategory category, string name, string context,
            string inspectorDescription = "", string roomId = "", bool isMacroClue = false)
        {
            Id                   = id;
            Category             = category;
            Name                 = name;
            Context              = context;
            InspectorDescription = inspectorDescription;
            RoomId               = roomId;
            IsMacroClue          = isMacroClue;
        }
    }
}
