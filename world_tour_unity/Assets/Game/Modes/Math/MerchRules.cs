using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>One line of the merch table, read from a decimals problem's params.lines (decimals.js:139).</summary>
    public sealed class MerchLine
    {
        public string Id, Name, Icon, Unit;
        public int Qty;
        public long Each, Cost;
        /// <summary>Tenths of a unit (bulk lines only), else -1 (JS null).</summary>
        public int Amount = -1;
    }

    /// <summary>
    /// world_tour/modes/math/merch.js without the drawing: the price tags and the receipt the worked hint draws, lined up
    /// on the decimal point.
    /// </summary>
    public static class MerchRules
    {
        static readonly string Times = ((char)0x00D7).ToString();

        public static List<MerchLine> Lines(Problem p)
        {
            var list = new List<MerchLine>();
            foreach (JsonValue l in p.Params["lines"].Items)
            {
                list.Add(new MerchLine
                {
                    Id = l["id"].String, Name = l["name"].String, Icon = l["icon"].String,
                    Qty = l["qty"].Int, Each = l["each"].Long, Cost = l["cost"].Long,
                    Amount = l["amount"].IsNull ? -1 : l["amount"].Int,
                    Unit = l["unit"].IsNull ? null : l["unit"].String,
                });
            }
            return list;
        }

        /// <summary>params.paid in cents; 0 when the fan doesn't pay (a total).</summary>
        public static long Paid(Problem p) { return p.Params["paid"].Long; }

        /// <summary>(l.amount / 10).toFixed(1) for a whole number of tenths: 25 is "2.5".</summary>
        public static string Tenths(int amount)
        {
            return (amount / 10).ToString(CultureInfo.InvariantCulture) + "." + (amount % 10).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>merch.js:10-12 the price tag: "2.5 ounces x $1.20", "3 x $2.15" or "$4.05".</summary>
        public static string Tag(MerchLine l)
        {
            if (l.Unit != null) return Tenths(l.Amount) + " " + l.Unit + "s " + Times + " " + Money.Dollars(l.Each);
            return l.Qty > 1 ? l.Qty.ToString(CultureInfo.InvariantCulture) + " " + Times + " " + Money.Dollars(l.Each) : Money.Dollars(l.Each);
        }

        /// <summary>merch.js:28 each item sticker's tilt (--r, degrees clockwise).</summary>
        public static readonly float[] ItemTilts = { -3f, 2f, -1.5f };

        /// <summary>One receipt row: the operator column (one character, or "") and the right-aligned amount.</summary>
        public struct ReceiptRow
        {
            public string Op, Amount;
            public bool Sum;
        }

        /// <summary>
        /// merch.js:15-23 the receipt: every amount padded on the left to the widest, so the points stack; the operator
        /// beside the last row; the "=" row shows the result once the answer is out (step 3), "?" until then.
        /// </summary>
        public static List<ReceiptRow> Receipt(JsonValue work, bool showResult)
        {
            List<long> rows = work["rows"].AsLongs();
            long result = work["result"].Long;
            string op = work["op"].String;
            int w = rows.Concat(new[] { result }).Max(c => Money.Dollars(c).Length);
            var list = new List<ReceiptRow>();
            for (int i = 0; i < rows.Count; i++)
                list.Add(new ReceiptRow { Op = i == rows.Count - 1 ? op : "", Amount = Money.Dollars(rows[i]).PadLeft(w, ' ') });
            list.Add(new ReceiptRow { Op = "=", Amount = showResult ? Money.Dollars(result).PadLeft(w, ' ') : "?".PadLeft(w, ' '), Sum = true });
            return list;
        }
    }
}
