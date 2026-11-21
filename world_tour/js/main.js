import * as store from './store.js';
import * as snd from './sound.js';
import { route, start } from './router.js';
import { mountSprite } from '../art/sprite.js';
import { sticker, icon } from '../art/sticker.js';
import { ticker, NAME } from './copy.js';
import { fmt } from './util.js';
import { applyLook } from './look.js';

const s = store.load();

/* what she has equipped backstage (theme, guitar, riff's outfit) + motion preference */
const root = document.documentElement;
applyLook();
if (s.settings.motion === 'reduced') root.dataset.motion = 'reduced';

mountSprite();

/* header */
const brand = document.getElementById('brand');
brand.innerHTML = `${sticker('guitar', { small: true })}<span class="full">${NAME}’s world tour</span><span class="short">world tour</span>`;
document.getElementById('ticker').textContent = ticker();

const fansEl = document.getElementById('hfans');
const fansIco = document.getElementById('hfans-ico');
fansIco.outerHTML = sticker('star', { small: true });
function paintFans() { fansEl.textContent = fmt(store.get().fans.lifetime); }
paintFans();
setInterval(paintFans, 1000);

/* sound toggle, remembered */
const sb = document.getElementById('sound');
snd.setEnabled(s.settings.sound);
function paintSound() {
  const on = store.get().settings.sound;
  sb.innerHTML = icon(on ? 'i-sound' : 'i-mute');
  sb.setAttribute('aria-pressed', String(on));
  sb.setAttribute('aria-label', on ? 'sound on' : 'sound off');
}
paintSound();
sb.onclick = () => {
  const st = store.get().settings; st.sound = !st.sound; store.save();
  snd.setEnabled(st.sound); paintSound(); if (st.sound) { snd.unlock(); snd.tap(); }
};

/* storage warning */
function noSave() { document.getElementById('nosave').hidden = false; }
if (!store.canSave()) noSave();
document.addEventListener('wt:nosave', noSave);

/* iPad Safari: audio only after a gesture; :active styles need a touch listener */
addEventListener('pointerdown', () => snd.unlock(), { once: true, passive: true });
document.addEventListener('touchstart', () => {}, { passive: true });

route('map', () => import('../screens/map.js'));
route('venue', () => import('../screens/venue.js'));
route('show', () => import('../screens/show.js'));
route('backstage', () => import('../screens/backstage.js'));
route('journal', () => import('../screens/journal.js'));
route('office', () => import('../screens/office.js'));
start();
