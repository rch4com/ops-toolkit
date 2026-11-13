const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const css = fs.readFileSync(path.join(__dirname, '..', 'tokens.css'), 'utf8');

function luminance(hex) {
  const values = hex.match(/[a-f\d]{2}/gi).map(v => parseInt(v, 16) / 255)
    .map(v => v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4);
  return values[0] * .2126 + values[1] * .7152 + values[2] * .0722;
}
function contrast(a, b) {
  const values = [luminance(a), luminance(b)].sort((a, b) => b - a);
  return (values[0] + .05) / (values[1] + .05);
}

for (const [name, block] of [['dark', css.split(':root, :root[data-theme="dark"] {')[1].split('}')[0]], ['light', css.split(':root[data-theme="light"] {')[1]]]) {
  test(`${name} text and interaction contrast`, () => {
    const tokens = Object.fromEntries([...block.matchAll(/--ut-([\w-]+):\s*(#[a-f\d]{6})/gi)].map(m => [m[1], m[2]]));
    for (const background of ['bg', 'surface', 'subtle', 'accent-soft']) {
      for (const foreground of ['text', 'muted', 'accent-text']) {
        assert.ok(contrast(tokens[foreground], tokens[background]) >= 4.5, `${foreground} on ${background}`);
      }
    }
    assert.ok(contrast(tokens.accent, tokens.surface) >= 3, 'focus outline');
    assert.ok(contrast(tokens.border, tokens.surface) >= 3, 'control boundary');
  });
}
test('native error text stays readable in both themes', () => {
  for (const [text, backgrounds] of [['#A1263D', ['#F8F9FE', '#FBE8ED']], ['#FFAFBA', ['#232532', '#482C37']]]) {
    for (const background of backgrounds) assert.ok(contrast(text, background) >= 4.5);
  }
});
