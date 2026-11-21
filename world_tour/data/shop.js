/* Backstage catalog. Everything here is cosmetic. Prices are in fans to spend (balance);
   buying never touches total fans, and nothing bought is ever taken away.
   A perfect 10-song show is about 40,000 fans; a typical one is 15,000 to 25,000.
   slot: theme | guitar | dog | sticker. The first item in each wearable slot is free and owned from the start. */
export const SHOP = [
  /* color themes: tokens.css has a [data-theme] block for each */
  { id: 'theme:tour-pink',  slot: 'theme',  name: 'tour pink',   price: 0,     swatch: ['#fdb1cb', '#000000', '#a8a9fc', '#ffffff'], blurb: 'the original. baby pink and ink.' },
  { id: 'theme:peach-navy', slot: 'theme',  name: 'peach + navy', price: 60000, swatch: ['#ffcfb5', '#1c2747', '#9fb3ea', '#fffaf5'], blurb: 'peach paper, navy ink. very tour poster.' },

  /* guitar skins: sets --gtr, the body color of every guitar sticker */
  { id: 'guitar:classic',   slot: 'guitar', name: 'classic',     price: 0,     color: 'var(--paper-deep)', blurb: 'matches the page.' },
  { id: 'guitar:lilac',     slot: 'guitar', name: 'lilac',       price: 12000, color: 'var(--accent)',     blurb: 'soft purple, loud amp.' },
  { id: 'guitar:sunburst',  slot: 'guitar', name: 'sunburst',    price: 18000, color: '#ffb23f',           blurb: 'orange like a stage light.' },
  { id: 'guitar:mint',      slot: 'guitar', name: 'mint',        price: 18000, color: '#8fe3c4',           blurb: 'fresh out of the case.' },
  { id: 'guitar:midnight',  slot: 'guitar', name: 'midnight',    price: 25000, color: '#2f2fc1',         blurb: 'deep blue, very serious.' },
  { id: 'guitar:cherry',    slot: 'guitar', name: 'cherry',      price: 30000, color: '#e8364f',           blurb: 'the rock-star red.' },

  /* outfits for riff: art/dog.js draws each layer */
  { id: 'dog:none',         slot: 'dog',    name: 'just riff',   price: 0,     blurb: 'band tee only. classic.' },
  { id: 'dog:bandana',      slot: 'dog',    name: 'bandana',     price: 15000, blurb: 'tied at the neck, road-trip style.' },
  { id: 'dog:beanie',       slot: 'dog',    name: 'beanie',      price: 20000, blurb: 'with a pompom. it’s cold on the bus.' },
  { id: 'dog:shades',       slot: 'dog',    name: 'shades',      price: 25000, blurb: 'too cool to look directly at.' },
  { id: 'dog:phones',       slot: 'dog',    name: 'headphones',  price: 30000, blurb: 'tiny, for checking the mix.' },
  { id: 'dog:collar',       slot: 'dog',    name: 'spiked collar', price: 35000, blurb: 'soft spikes. riff is a punk now.' },

  /* stickers for the tour journal (milestone stickers are earned, not sold) */
  { id: 'sticker:pick',     slot: 'sticker', sticker: 'pick',     name: 'guitar pick', price: 5000, blurb: 'always lost, always needed.' },
  { id: 'sticker:note',     slot: 'sticker', sticker: 'note',     name: 'music note',  price: 5000, blurb: 'a little doodle.' },
  { id: 'sticker:coffee',   slot: 'sticker', sticker: 'coffee',   name: 'coffee cup',  price: 6000, blurb: 'from where it all started.' },
  { id: 'sticker:phones',   slot: 'sticker', sticker: 'phones',   name: 'headphones',  price: 8000, blurb: 'for the bus ride home.' },
  { id: 'sticker:tee',      slot: 'sticker', sticker: 'tee',      name: 'merch tee',   price: 8000, blurb: 'sold out in every size.' },
  { id: 'sticker:crate',    slot: 'sticker', sticker: 'crate',    name: 'speaker crate', price: 10000, blurb: 'fragile. this side up.' },
  { id: 'sticker:pass',     slot: 'sticker', sticker: 'pass',     name: 'backstage pass', price: 12000, blurb: 'all access.' }
];

export const SLOTS = [
  { id: 'theme',   name: 'color themes', note: 'changes the whole game' },
  { id: 'guitar',  name: 'guitar skins', note: 'every guitar sticker' },
  { id: 'dog',     name: 'riff’s outfits', note: 'riff wears it everywhere' },
  { id: 'sticker', name: 'journal stickers', note: 'yours to stick in the tour journal' }
];

export const itemById = id => SHOP.find(i => i.id === id) || null;
