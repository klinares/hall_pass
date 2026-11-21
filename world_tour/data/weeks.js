/* =========================================================
   WORD BANK: add each new week at the TOP.
   id    = test date (YYYY-MM-DD). The newest week on or before
           today opens automatically; older weeks stay in the menu.
   w     = the word
   s     = syllable chunks (hint after a second miss)
   h     = definition / clue
   x     = (optional) a sentence with ___ where the word goes, for lyric writer
   prefix= (optional) { p: prefix, root: the word it's added to, means: what the prefix adds }
           prefix mixer only shows for weeks with 4+ words that have this.
   ========================================================= */
export const WEEKS = [
  { id:"2026-10-26", label:"week of oct 26, 2026", words:[
    { w:"irrational",    s:"ir-ra-tion-al",     h:"Not based on reason or clear thinking, like a fear that makes no sense",
      x:"i know the tiny spider can't hurt me, so being this scared of it is totally ___.",
      prefix:{ p:"ir", root:"rational", means:"not" } },
    { w:"intercept",     s:"in-ter-cept",       h:"To stop or catch something on its way from one place to another, like a football pass",
      x:"the goalie leaped to ___ the ball before it hit the net." },
    { w:"illegal",       s:"il-le-gal",         h:"Against the law",
      x:"it's ___ to park the tour bus in front of a fire hydrant.",
      prefix:{ p:"il", root:"legal", means:"not" } },
    { w:"international", s:"in-ter-na-tion-al", h:"Happening between or involving two or more countries",
      x:"the band's first ___ show was in a city across the ocean.",
      prefix:{ p:"inter", root:"national", means:"between" } },
    { w:"irresponsible", s:"ir-re-spon-si-ble", h:"Not able to be trusted to do what you're supposed to do; careless about your duties",
      x:"leaving the puppy alone with a whole pizza was pretty ___.",
      prefix:{ p:"ir", root:"responsible", means:"not" } },
    { w:"illegible",     s:"il-leg-i-ble",      h:"So messy that it can't be read, like scribbled handwriting",
      x:"the setlist got rained on, and now it's totally ___.",
      prefix:{ p:"il", root:"legible", means:"not" } },
    { w:"intersection",  s:"in-ter-sec-tion",   h:"The spot where two roads or lines cross each other",
      x:"the tour bus waited at a busy ___ while people crossed.",
      prefix:{ p:"inter", root:"section", means:"between" } },
    { w:"irregular",     s:"ir-reg-u-lar",      h:"Not even, normal, or following the usual pattern",
      x:"the drummer's beat went ___ when she lost count.",
      prefix:{ p:"ir", root:"regular", means:"not" } },
    { w:"interaction",   s:"in-ter-ac-tion",    h:"When two or more people or things talk, work, or act together and affect each other",
      x:"her favorite part of the night is the ___ with fans after the show.",
      prefix:{ p:"inter", root:"action", means:"between" } },
    { w:"illogical",     s:"il-log-i-cal",      h:"Not following the steps of good reasoning, like an argument whose conclusion doesn't follow",
      x:"“it rained, so the show sold out” is an ___ argument. one thing doesn't lead to the other.",
      prefix:{ p:"il", root:"logical", means:"not" } },
    { w:"irreplaceable", s:"ir-re-place-a-ble", h:"So special or valuable that nothing else could take its place",
      x:"her first guitar is ___ to her, scratches and all.",
      prefix:{ p:"ir", root:"replaceable", means:"not" } },
    { w:"intercede",     s:"in-ter-cede",       h:"To step in and speak up for someone else, often to help settle a problem",
      x:"when the twins started arguing, their big sister had to ___." }
  ]},
  { id:"2026-10-10", label:"week of oct 10, 2026", words:[
    { w:"population",   s:"pop-u-la-tion",    h:"The total number of people living in a specific area (like a massive concert crowd!)",
      x:"the town's ___ doubled the weekend of the big music festival." },
    { w:"tropical",     s:"trop-i-cal",       h:"Warm, humid regions near the Earth's equator where it's sunny all year",
      x:"the beach stage had palm trees and a warm ___ breeze." },
    { w:"pyramid",      s:"pyr-a-mid",        h:"A massive ancient structure with triangular sides, like those built in Egypt",
      x:"the ancient ___ had four triangle-shaped sides that met at a point." },
    { w:"empire",       s:"em-pire",          h:"A large group of territories or nations ruled by a single leader or government",
      x:"the ___ kept growing until it ruled lands on three continents." },
    { w:"peninsula",    s:"pen-in-su-la",     h:"A piece of land almost entirely surrounded by water, sticking out into the sea",
      x:"florida is a ___, with water on three sides." },
    { w:"civilization", s:"civ-i-li-za-tion", h:"A complex human society with its own advanced culture, cities, and government",
      x:"the ancient maya built a ___ with cities, farms, and calendars." },
    { w:"rainforest",   s:"rain-for-est",     h:"A dense, lush forest found in wet, warm areas with heavy rainfall",
      x:"it rains almost every day in the ___, so everything stays green." },
    { w:"indigenous",   s:"in-dig-e-nous",    h:"Originating or occurring naturally in a particular place; native to the land",
      x:"the ___ people of this land lived here long before anyone else arrived." },
    { w:"temple",       s:"tem-ple",          h:"A sacred building devoted to the worship of a god or gods in ancient cultures",
      x:"people came to the ___ to pray and leave gifts for their gods." },
    { w:"monument",     s:"mon-u-ment",       h:"A statue, building, or structure built to remember a famous person or historical event",
      x:"the city built a ___ to honor the people who founded it." }
  ]}
];
