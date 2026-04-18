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
        public Rectangle    Bounds         { get; }

        public DeductionSlot(ClueCategory category, string correctClueId, Rectangle bounds)
        {
            Category      = category;
            CorrectClueId = correctClueId;
            Bounds        = bounds;
        }
    }

    public sealed class DeductionManager
    {
        public List<DeductionSlot> Slots             { get; }
        public string              ValidationMessage { get; set; } = "";

        /// <param name="slots">
        ///   Correct-answer slots loaded from <c>level_config.json</c>.
        /// </param>
        public DeductionManager(List<DeductionSlot> slots)
        {
            Slots = slots;
        }

        /// <summary>
        /// Returns true only when every slot matches its correct answer.
        /// Sets <see cref="ValidationMessage"/> in all cases.
        /// </summary>
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
                if (slot.SelectedClueId != slot.CorrectClueId)
                {
                    ValidationMessage = "Incorrect logic.";
                    return false;
                }
            }
            ValidationMessage = "Correct!";
            return true;
        }
    }
}
