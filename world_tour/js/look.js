/* Applies what she has equipped backstage: color theme, guitar skin, riff's outfit. */
import * as store from './store.js';
import { look } from './meta.js';
import { setOutfit } from '../art/dog.js';

export function applyLook() {
  const L = look(store.get()), root = document.documentElement;
  if (L.theme === 'tour-pink') delete root.dataset.theme; else root.dataset.theme = L.theme;
  root.style.setProperty('--gtr', L.guitar);
  setOutfit(L.outfit);
  const paper = getComputedStyle(root).getPropertyValue('--paper').trim();
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta && paper) meta.setAttribute('content', paper);
  return L;
}
