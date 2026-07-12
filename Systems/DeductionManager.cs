using System;
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

        /// <param name="correctClueIds">Answer clue ids, matched to slots in parse order.</param>
        /// <param name="categoryLookup">
        ///   Resolves a clue id to its category so slots typed with content tags
        ///   (e.g. "[Police Lockdown]") take the category of their answer clue.
        /// </param>
        public DeductionManager(string sentence,
            IReadOnlyList<string>? correctClueIds = null,
            Func<string, ClueCategory?>? categoryLookup = null)
        {
            Slots    = new List<DeductionSlot>();
            Segments = ParseSentence(sentence, correctClueIds, categoryLookup);
        }

        private List<SentenceSegment> ParseSentence(string sentence,
            IReadOnlyList<string>? correctClueIds,
            Func<string, ClueCategory?>? categoryLookup)
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

                string tag = sentence[(open + 1)..close].Trim();

                string correctId = correctClueIds != null && Slots.Count < correctClueIds.Count
                    ? correctClueIds[Slots.Count]
                    : "";

                // Prefer the answer clue's category: guarantees the correct clue is
                // always insertable into its slot regardless of how the tag is phrased.
                ClueCategory category =
                    (correctId != "" ? categoryLookup?.Invoke(correctId) : null)
                    ?? TagToCategory(tag.ToUpperInvariant());

                var slot = new DeductionSlot(category, correctId, tag);
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

        /// <summary>Sentence with each slot replaced by the display name of its filled clue.</summary>
        public string BuildFilledSentence(Func<string, string?> clueNameLookup)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var seg in Segments)
            {
                if (seg is TextSegment ts)
                    sb.Append(ts.Text);
                else if (seg is SlotSegment ss)
                    sb.Append(clueNameLookup(ss.Slot.SelectedClueId ?? "") ?? ss.Slot.TagLabel);
            }
            return sb.ToString();
        }

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
            int checkedSlots = 0, correct = 0;
            foreach (var slot in Slots)
            {
                if (string.IsNullOrEmpty(slot.CorrectClueId)) continue;
                checkedSlots++;
                if (slot.SelectedClueId == slot.CorrectClueId) correct++;
            }
            if (correct < checkedSlots)
            {
                // Partial feedback turns a wrong submit into a deduction step
                // instead of pure trial-and-error (playtest: solve order felt
                // arbitrary with no signal which pieces already fit).
                ValidationMessage = $"Incorrect logic - {correct}/{checkedSlots} fit.";
                return false;
            }
            ValidationMessage = "Case closed!";
            return true;
        }
    }
}
