using System.Collections.Generic;
using WorldTour.Game.Show;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: missing vowels (world_tour/modes/spelling/vowels.js). Every vowel is gone (_mp_r_). Put them back.
    /// Same typed answer, ladder and letter hints as missing letters.
    /// </summary>
    public sealed class VowelsMode : MissingLettersMode
    {
        protected override string ModeId { get { return "vowels"; } }
        protected override string DeskTag { get { return SpellingLines.VowelsDeskTag; } }
        protected override char Blank { get { return SpellingLines.VowelsBlank; } }

        /// <summary>vowels.js:136.</summary>
        protected override List<int> Hide(SongContext ctx)
        {
            return MissingLetters.VowelsHide(ctx.Item.W);
        }
    }
}
