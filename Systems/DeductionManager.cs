using System.Collections.Generic;
using CatDetective.Entities;
using Microsoft.Xna.Framework;

namespace CatDetective.Systems
{
    public sealed class DeductionSlot
    {
        public ClueCategory Category       { get; }
        public string       CorrectClueId  { get; }
        public string?      SelectedClueId { get; set; }
        public Rectangle    Bounds         { get; set; }
        public string       TagLabel       { get; }

        public DeductionSlot(ClueCategory category, string correctClueId, string tagLabel)
        {
            Category      = category;
            CorrectClueId = correctClueId;
            TagLabel      = tagLabel;
        }
    }

    public abstract class SentenceSegment { }

    public sealed class TextSegment : SentenceSegment
    {
        public string Text { get; }
        public TextSegment(string text) => Text = text;
    }

    public sealed class SlotSegment : SentenceSegment
    {
        public DeductionSlot Slot { get; }
        public SlotSegment(DeductionSlot slot) => Slot = slot;
    }

    public sealed class DeductionManager
    {
        public List<SentenceSegment> Segments          { get; }
        public List<DeductionSlot>   Slots             { get; }
        public string                ValidationMessage { get; set; } = "";

        public DeductionManager(string sentence)
        {
            Slots    = new List<DeductionSlot>();
            Segments = ParseSentence(sentence);
        }

        private List<SentenceSegment> ParseSentence(string sentence)
        {
            var segments = new List<SentenceSegment>();
            int i = 0;
            while (i < sentence.Length)
            {
                int open = sentence.IndexOf('[', i);
                if (open == -1)
                {
                    if (i < sentence.Length)
                        segments.Add(new TextSegment(sentence[i..]));
                    break;
                }
                if (open > i)
                    segments.Add(new TextSegment(sentence[i..open]));

                int close = sentence.IndexOf(']', open + 1);
                if (close == -1)
                {
                    segments.Add(new TextSegment(sentence[open..]));
                    break;
                }

                string tag      = sentence[(open + 1)..close].Trim();
                var    category = TagToCategory(tag.ToUpperInvariant());
                var    slot     = new DeductionSlot(category, "", tag);
                Slots.Add(slot);
                segments.Add(new SlotSegment(slot));
                i = close + 1;
            }
            return segments;
        }

        private static ClueCategory TagToCategory(string tag) => tag switch
        {
            "WHO"       => ClueCategory.Who,
            "WHAT"      => ClueCategory.What,
            "HOW"       => ClueCategory.What,
            "WHY"       => ClueCategory.Why,
            "WHERE"     => ClueCategory.WhereWhen,
            "WHEREWHEN" => ClueCategory.WhereWhen,
            "WHEN"      => ClueCategory.WhereWhen,
            _           => ClueCategory.Who,
        };

        public bool ValidateCase()
        {
            foreach (var slot in Slots)
            {
                if (slot.SelectedClueId == null)
                {
                    ValidationMessage = "Fill all slots!";
                    return false;
                }
            }
            foreach (var slot in Slots)
            {
                if (!string.IsNullOrEmpty(slot.CorrectClueId) &&
                    slot.SelectedClueId != slot.CorrectClueId)
                {
                    ValidationMessage = "Incorrect logic.";
                    return false;
                }
            }
            ValidationMessage = "Case closed!";
            return true;
        }
    }
}
