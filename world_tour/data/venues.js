/* The tour route. Lifetime fans unlock the next venue. All venues and names are fictional.
   crowd: 0..1, scales the crowd swell at the end of a show. */
export const VENUES = [
  { id:'coffee',   art:'coffee',   name:'the corner coffee shop', short:'coffee shop',    fans:0,       crowd:.15, blurb:'open mic night. twelve people and a barista. everyone starts here.' },
  { id:'records',  art:'records',  name:'record store basement',  short:'record store',   fans:20000,   crowd:.25, blurb:'low ceiling, loud amps, a cat asleep on the bass case.' },
  { id:'rec',      art:'rec',      name:'rec center battle of the bands', short:'rec center', fans:60000, crowd:.35, blurb:'folding chairs, a scoreboard, and very serious judges.' },
  { id:'rooftop',  art:'rooftop',  name:'rooftop party',          short:'rooftop',        fans:125000,  crowd:.45, blurb:'string lights, city skyline, someone’s aunt filming everything.' },
  { id:'club',     art:'club',     name:'the fuzz, downtown',      short:'the club',       fans:225000,  crowd:.6,  blurb:'a real stage, a real sound guy, a line around the block.' },
  { id:'theater',  art:'theater',  name:'the old movie palace',    short:'theater',        fans:375000,  crowd:.72, blurb:'velvet seats, gold balconies, your name on the marquee.' },
  { id:'festival', art:'festival', name:'summer festival side stage', short:'festival',   fans:600000,  crowd:.86, blurb:'mud, glitter, and thousands of people who just found you.' },
  { id:'arena',    art:'arena',    name:'the arena',               short:'the arena',      fans:900000,  crowd:1,   blurb:'the big one. lights, screens, and a whole city singing along.' }
];

export const venueById = id => VENUES.find(v => v.id === id) || VENUES[0];
