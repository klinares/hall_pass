using System.Collections.Generic;
using WorldTour.Game.Show;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: missing letters (world_tour/modes/spelling/slots.js). About a third of the letters are blanked (more at
    /// higher levels, never all of them); she types the whole word. Hints: syllables first, then one blank filled per
    /// press, held to the QUESTIONS 11 floor. Misses follow the spelling ladder; the third shows the word.
    /// </summary>
    public sealed class SlotsMode : MissingLettersMode
    {
        protected override string ModeId { get { return "slots"; } }
        protected override string DeskTag { get { return SpellingLines.SlotsDeskTag; } }
        protected override char Blank { get { return SpellingLines.SlotsBlank; } }

        /// <summary>slots.js:110-111.</summary>
        protected override List<int> Hide(SongContext ctx)
        {
            return MissingLetters.SlotsHide(ctx.Item.W, ctx.Level, ctx.Random);
        }
    }
}
