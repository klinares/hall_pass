using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// MERCH TABLE: adding, subtracting and multiplying decimals with money (5.NBT.7). Ported from
    /// world_tour/math/gen/decimals.js; every RNG call happens in the same order, so problems match the JS.
    /// L1: 2 items under $10, the total or the change from $10.
    /// L2: 3 items, one bought as quantity x price, the total or the change from $20.
    /// L3: decimal x decimal (2.5 ounces at $1.20 an ounce), alone or with change from $50.
    /// </summary>
    public static class Decimals
    {
        public const string Skill = "dec";

        /// <summary>A merch item (JS ITEMS entry): prices in cents between Lo and Hi.</summary>
        public sealed class Item
        {
            public readonly string Id, Name, A, Pl, Icon;
            public readonly int Lo, Hi;

            public Item(string id, string name, string a, string pl, string icon, int lo, int hi)
            {
                Id = id; Name = name; A = a; Pl = pl; Icon = icon; Lo = lo; Hi = hi;
            }
        }

        /// <summary>Sold by weight or length, for decimal x decimal (JS BULK entry).</summary>
        public sealed class Bulk
        {
            public readonly string Id, Name, Unit, An, Icon;

            public Bulk(string id, string name, string unit, string an, string icon)
            {
                Id = id; Name = name; Unit = unit; An = an; Icon = icon;
            }
        }

        public static readonly IReadOnlyList<Item> Items = new[]
        {
            new Item("tee", "band tee", "a", "band tees", "tee", 900, 1500),
            new Item("pin", "enamel pin", "an", "enamel pins", "star", 150, 450),
            new Item("stickers", "sticker sheet", "a", "sticker sheets", "heart", 105, 350),
            new Item("picks", "pick pack", "a", "pick packs", "pick", 150, 400),
            new Item("poster", "tour poster", "a", "tour posters", "setlist", 300, 650),
            new Item("band", "glow wristband", "a", "glow wristbands", "bolt", 105, 300),
            new Item("tape", "cassette", "a", "cassettes", "cassette", 350, 650),
            new Item("vinyl", "mini vinyl", "a", "mini vinyls", "vinyl", 450, 1200),
            new Item("phones", "pair of earplugs", "a", "pairs of earplugs", "phones", 105, 250)
        };

        public static readonly IReadOnlyList<Bulk> BulkItems = new[]
        {
            new Bulk("glitter", "face glitter", "ounce", "an", "star"),
            new Bulk("ribbon", "merch ribbon", "yard", "a", "heart"),
            new Bulk("candy", "tour-bus candy", "pound", "a", "coffee")
        };

        /// <summary>One line on the order slip. A bulk line has Unit, Amount (tenths) and Cost and no Qty.</summary>
        private sealed class Line
        {
            public string Id, Name, A, Pl, Icon;
            public int Qty;           // 0 = the JS has no qty (bulk lines)
            public long Each;
            public string Unit, An;   // bulk only
            public int Amount;        // bulk only: tenths of a unit
            public long Cost;         // bulk only
            public bool IsBulk => Unit != null;

            public static Line Of(Item it, int qty, long each) =>
                new Line { Id = it.Id, Name = it.Name, A = it.A, Pl = it.Pl, Icon = it.Icon, Qty = qty, Each = each };
        }

        // JS: price = (r, lo, hi, step) => lo + step * r.int(0, Math.floor((hi - lo) / step))
        private static long Price(Rng r, long lo, long hi, long step) =>
            lo + step * r.Int(0, (int)Math.Floor((double)(hi - lo) / step));

        // JS: fits(lo, hi)(it) => Math.max(lo, it.lo) <= Math.min(hi, it.hi)
        private static Func<Item, bool> Fits(int lo, int hi) => it => Math.Max(lo, it.Lo) <= Math.Min(hi, it.Hi);

        // JS: priceFor = (r, it, lo, hi, step) => price(r, Math.ceil(Math.max(lo, it.lo) / step) * step, Math.min(hi, it.hi), step)
        private static long PriceFor(Rng r, Item it, int lo, int hi, int step) =>
            Price(r, (long)Math.Ceiling((double)Math.Max(lo, it.Lo) / step) * step, Math.Min(hi, it.Hi), step);

        private static string D(long cents) => Money.Dollars(cents);
        private static string S(long v) => v.ToString(CultureInfo.InvariantCulture);
        // JS (amount / 10).toFixed(1) for a whole number of tenths
        private static string Tenths(int amount) => S(amount / 10) + "." + S(amount % 10);

        private static readonly Regex LastComma = new Regex(", ([^,]*)\\z", RegexOptions.CultureInvariant);

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            string kind;
            List<Line> lines;
            long paid = 0, total;
            Answer answer;
            var traps = new List<Trap>();
            List<string> steps;
            string prompt, label;

            if (level == 1)
            {
                List<Item> pool = r.Shuffle(Items.Where(Fits(105, 475)).ToList());
                lines = new List<Line>();
                for (int i = 0; i < 2; i++) lines.Add(Line.Of(pool[i], 1, PriceFor(r, pool[i], 105, 475, 5)));
                total = lines[0].Each + lines[1].Each;
                kind = r.Chance(.5) ? "total" : "change";
            }
            else if (level == 2)
            {
                for (;;)
                {
                    Item multi = r.Pick(Items.Where(Fits(110, 395)).ToList());
                    List<Item> rest = r.Shuffle(Items.Where(it => it != multi && Fits(105, 650)(it)).ToList());
                    int qty = r.Int(2, 4);
                    lines = new List<Line> { Line.Of(multi, qty, PriceFor(r, multi, 110, 395, 5)) };
                    lines.Add(Line.Of(rest[0], 1, PriceFor(r, rest[0], 105, 650, 1)));
                    lines.Add(Line.Of(rest[1], 1, PriceFor(r, rest[1], 105, 650, 1)));
                    total = lines.Sum(l => l.Qty * l.Each);
                    if (total < 2000) break;
                }
                kind = r.Chance(.5) ? "total" : "change";
            }
            else
            {
                Bulk b = r.Pick(BulkItems);
                int tenths;
                do tenths = r.Int(11, 49); while (tenths % 10 == 0);
                long per = Price(r, 110, 490, 10); // a whole number of dimes, so the cost lands on whole cents
                var bulk = new Line { Id = b.Id, Name = b.Name, Unit = b.Unit, An = b.An, Icon = b.Icon, Amount = tenths, Each = per, Cost = tenths * per / 10 };
                lines = new List<Line> { bulk };
                kind = r.Chance(.5) ? "decmul" : "change";
                if (kind == "change")
                {
                    Item it = r.Pick(Items.Where(Fits(250, 1500)).ToList());
                    lines.Add(Line.Of(it, 1, PriceFor(r, it, 250, 1500, 5)));
                }
                total = bulk.Cost + (lines.Count > 1 ? lines[1].Each : 0);
            }

            // JS: lineCost = l => l.cost ?? l.qty * l.each
            Func<Line, long> lineCost = l => l.IsBulk ? l.Cost : l.Qty * l.Each;
            List<long> costs = lines.Select(lineCost).ToList();

            if (kind == "change")
            {
                paid = level == 1 ? 1000 : level == 2 ? 2000 : 5000;
                answer = Answer.Money(paid - total);
                traps.Add(new Trap(MathValue.Num(total), "gave-total"));
                traps.Add(new Trap(MathValue.Num(Money.NoBorrowSub(paid, total)), "no-borrow"));
                traps.Add(new Trap(MathValue.Num(paid - Money.NoCarryAdd(costs)), "no-carry"));
            }
            else if (kind == "decmul")
            {
                answer = Answer.Money(total);
                traps.Add(new Trap(MathValue.Num(total * 10), "decimal-place"));
                traps.Add(new Trap(total % 10 == 0 ? MathValue.Num(total / 10) : MathValue.Null, "decimal-place"));
                traps.Add(new Trap(MathValue.Num(lines[0].Amount * 10 + lines[0].Each), "added-instead"));
            }
            else
            {
                answer = Answer.Money(total);
                traps.Add(new Trap(MathValue.Num(Money.NoCarryAdd(costs)), "no-carry"));
            }

            // the words on the order slip
            Func<Line, string> lineText = l => l.IsBulk
                ? Tenths(l.Amount) + " " + l.Unit + "s of " + l.Name + " at " + D(l.Each) + " " + l.An + " " + l.Unit
                : l.Qty > 1 ? S(l.Qty) + " " + l.Pl + " at " + D(l.Each) + " each" : l.A + " " + l.Name + " for " + D(l.Each);
            Func<string> slip = () => LastComma.Replace(string.Join(", ", lines.Select(lineText)), " and $1", 1);

            if (kind == "change")
            {
                prompt = "a fan buys " + slip() + ". they pay with " + D(paid) + ". how much change?";
                label = "change from " + D(paid);
            }
            else if (kind == "decmul")
            {
                prompt = "a fan wants " + lineText(lines[0]) + ". what does it cost?";
                label = Tenths(lines[0].Amount) + " " + Glyph.Times + " " + D(lines[0].Each);
            }
            else
            {
                prompt = "a fan buys " + slip() + ". what" + Glyph.Apos + "s the total?";
                label = "total of " + S(lines.Count) + " items";
            }

            // hint ladder: nudge, partial step, worked solution
            Line qtyLine = lines.FirstOrDefault(l => l.Qty > 1);
            Line bulkLine = lines.FirstOrDefault(l => l.IsBulk);
            var partials = new List<string>();
            if (qtyLine != null)
                partials.Add(S(qtyLine.Qty) + " " + Glyph.Times + " " + D(qtyLine.Each) + " = <b>" + D(qtyLine.Qty * qtyLine.Each) + "</b>");
            if (bulkLine != null)
                partials.Add(S(bulkLine.Amount) + " " + Glyph.Times + " " + S(bulkLine.Each) + " = " + S(bulkLine.Amount * bulkLine.Each) + ". " +
                             Tenths(bulkLine.Amount) + " has 1 decimal place and " + D(bulkLine.Each).Substring(1) + " has 2, so put back 3: <b>" + D(bulkLine.Cost) + "</b>");
            string totalLine = string.Join(" + ", costs.Select(D)) + " = <b>" + D(total) + "</b>";

            if (kind == "change")
            {
                steps = new List<string>
                {
                    "change is what they paid minus what it costs. find the total first" +
                        (qtyLine != null ? ", starting with the " + qtyLine.Pl : bulkLine != null ? ", starting with the " + bulkLine.Name : "") + ".",
                    partials.Count > 0
                        ? string.Join(". ", partials) + (costs.Count > 1 ? ". total: " + totalLine + "." : ".")
                        : "the total is " + totalLine + ". now take that away from " + D(paid) + ".",
                    D(paid) + " " + Glyph.Minus + " " + D(total) + " = <b>" + D(paid - total) + "</b>"
                };
            }
            else if (kind == "decmul")
            {
                steps = new List<string>
                {
                    "multiply like whole numbers first: " + S(bulkLine.Amount) + " " + Glyph.Times + " " + S(bulkLine.Each) + ". then put the decimal point back.",
                    S(bulkLine.Amount) + " " + Glyph.Times + " " + S(bulkLine.Each) + " = " + S(bulkLine.Amount * bulkLine.Each) +
                        ". count the decimal places in " + Tenths(bulkLine.Amount) + " and " + D(bulkLine.Each).Substring(1) + ".",
                    partials[0]
                };
            }
            else
            {
                string second;
                if (qtyLine != null) second = partials[0] + ". now add the other items.";
                else
                {
                    long c = costs.Sum(x => x % 100);
                    second = c >= 100
                        ? "the cents make " + S(c) + Glyph.Cent + ". that" + Glyph.Apos + "s " + S((long)Math.Floor(c / 100.0)) + " extra dollar" + (c >= 200 ? "s" : "") +
                          (c % 100 != 0 ? " and " + S(c % 100) + Glyph.Cent : "") + "."
                        : "the cents make " + S(c) + Glyph.Cent + ", less than a dollar, so nothing carries. now the dollars.";
                }
                steps = new List<string>
                {
                    qtyLine != null
                        ? "start with the " + S(qtyLine.Qty) + " " + qtyLine.Pl + ": that" + Glyph.Apos + "s " + S(qtyLine.Qty) + " " + Glyph.Times + " " + D(qtyLine.Each) + "."
                        : "line up the decimal points and add the cents first.",
                    second,
                    totalLine
                };
            }

            var lineJson = new List<JsonValue>();
            foreach (Line l in lines)
            {
                lineJson.Add(JsonValue.Obj()
                    .Set("id", l.Id).Set("name", l.Name).Set("icon", l.Icon)
                    .Set("qty", l.Qty != 0 ? l.Qty : 1)             // JS: l.qty || 1
                    .Set("each", l.Each)
                    .Set("amount", l.IsBulk ? JsonValue.Num(l.Amount) : JsonValue.Null)
                    .Set("unit", l.IsBulk ? JsonValue.Str(l.Unit) : JsonValue.Null)
                    .Set("cost", lineCost(l)));
            }

            JsonValue work;
            if (kind == "change") work = JsonValue.Obj().Set("op", Glyph.Minus).Set("rows", JsonValue.Ints(new[] { paid, total })).Set("result", paid - total);
            else if (kind == "decmul") work = JsonValue.Null;
            else work = JsonValue.Obj().Set("op", "+").Set("rows", JsonValue.Ints(costs)).Set("result", total);

            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = JsonValue.Obj().Set("lines", JsonValue.Arr(lineJson)).Set("paid", paid).Set("total", total),
                Prompt = prompt, Label = label, Answer = answer,
                Simplest = false,
                Choices = null,
                Traps = Mistakes.CleanTraps(answer, traps),
                Steps = steps,
                // the column sum the worked hint draws
                Work = work
            };
        }
    }
}
