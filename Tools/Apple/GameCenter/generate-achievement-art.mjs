import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../../AppStore/Apple/GameCenter/Achievements/Source");
fs.mkdirSync(root, { recursive: true });

const C = {
  bg: "#F7F1E3",
  ink: "#27313B",
  blue: "#4D8BCE",
  green: "#62A66F",
  red: "#D85E5E",
  yellow: "#E6B84D",
  white: "#FFFDF8"
};

const text = (x, y, value, size, fill = C.white) =>
  `<text x="${x}" y="${y}" text-anchor="middle" dominant-baseline="middle" font-family="Arial, Helvetica, sans-serif" font-size="${size}" font-weight="700" fill="${fill}">${value}</text>`;

const tile = (x, y, size, fill, value, fontSize = 190) =>
  `<rect x="${x}" y="${y}" width="${size}" height="${size}" rx="56" fill="${fill}" stroke="${C.ink}" stroke-width="20"/>` +
  (value ? text(x + size / 2, y + size / 2 + 7, value, fontSize) : "");

const circle = (cx, cy, r, fill = C.white, width = 20) =>
  `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" stroke="${C.ink}" stroke-width="${width}"/>`;

const svg = body => `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
<rect width="1024" height="1024" fill="${C.bg}"/>
<rect x="48" y="48" width="928" height="928" rx="112" fill="none" stroke="${C.ink}" stroke-width="18"/>
${body}
</svg>
`;

const art = {
  "adventure-begins": svg(
    tile(172, 252, 380, C.green, "1", 220) +
    `<path d="M580 708 C650 690 648 586 714 564 C780 542 786 438 852 408" fill="none" stroke="${C.ink}" stroke-width="30" stroke-linecap="round"/>` +
    circle(578, 708, 22, C.yellow, 14) +
    `<path d="M808 330 L874 366 L808 402 Z" fill="${C.red}" stroke="${C.ink}" stroke-width="16" stroke-linejoin="round"/><path d="M810 326 V476" stroke="${C.ink}" stroke-width="20" stroke-linecap="round"/>`
  ),
  "seasoned-explorer": svg(
    tile(182, 176, 440, C.blue, "10", 190) +
    `<path d="M220 790 C300 650 430 820 530 696 C634 566 734 738 834 570" fill="none" stroke="${C.ink}" stroke-width="28" stroke-linecap="round"/>` +
    circle(220, 790, 24, C.green, 14) + circle(530, 696, 24, C.yellow, 14) + circle(834, 570, 24, C.red, 14)
  ),
  "free-thinker": svg(
    tile(154, 266, 230, C.blue, "") +
    tile(397, 168, 230, C.yellow, "") +
    tile(640, 300, 230, C.red, "") +
    tile(390, 536, 246, C.green, "?", 174) +
    `<path d="M512 104 V54 M746 174 L790 126 M280 166 L238 118" stroke="${C.ink}" stroke-width="22" stroke-linecap="round"/>`
  ),
  "pack-it-up": svg(
    tile(234, 190, 360, C.blue, "") +
    tile(330, 286, 360, C.yellow, "") +
    tile(426, 382, 360, C.red, "5", 220) +
    `<path d="M198 798 H826" stroke="${C.ink}" stroke-width="30" stroke-linecap="round"/>`
  ),
  "triple-threat": svg(
    tile(116, 338, 240, C.green, "1", 145) +
    tile(392, 338, 240, C.yellow, "2", 145) +
    tile(668, 338, 240, C.red, "3", 145) +
    `<path d="M194 682 L246 734 L340 630 M470 682 L522 734 L616 630 M746 682 L798 734 L892 630" fill="none" stroke="${C.ink}" stroke-width="26" stroke-linecap="round" stroke-linejoin="round"/>`
  ),
  "perfect-week": svg(
    tile(322, 180, 380, C.green, "7", 230) +
    [0,1,2,3,4,5,6].map((i) => circle(218 + i * 98, 720, 31, i === 6 ? C.yellow : C.white, 16)).join("") +
    `<path d="M218 808 H806" stroke="${C.ink}" stroke-width="22" stroke-linecap="round"/>`
  ),
  "against-the-clock": svg(
    circle(512, 500, 224, C.white, 26) +
    `<path d="M512 500 V350 M512 500 L634 568" stroke="${C.ink}" stroke-width="28" stroke-linecap="round"/>` +
    circle(512, 500, 18, C.ink, 0) +
    tile(126, 178, 156, C.blue, "3", 92) +
    tile(742, 178, 156, C.green, "4", 92) +
    tile(126, 690, 156, C.yellow, "5", 92) +
    tile(742, 690, 156, C.red, "6", 92)
  ),
  "clockwork": svg(
    `<rect x="442" y="126" width="140" height="82" rx="24" fill="${C.yellow}" stroke="${C.ink}" stroke-width="20"/>` +
    circle(512, 518, 294, C.red, 26) +
    text(512, 536, "300", 190) +
    `<path d="M512 270 V224 M730 316 L782 264" stroke="${C.ink}" stroke-width="26" stroke-linecap="round"/>`
  ),
  "century-club": svg(
    tile(208, 176, 440, C.blue, "100", 172) +
    Array.from({length: 10}, (_, i) => circle(236 + i * 62, 744, 18, i < 5 ? C.green : C.yellow, 10)).join("")
  ),
  "shikaku-master": svg(
    `<path d="M236 430 L314 234 L452 354 L512 178 L572 354 L710 234 L788 430 Z" fill="${C.yellow}" stroke="${C.ink}" stroke-width="24" stroke-linejoin="round"/>` +
    tile(212, 456, 600, C.red, "1000", 176)
  )
};

for (const [name, contents] of Object.entries(art)) {
  fs.writeFileSync(path.join(root, `${name}.svg`), contents, "utf8");
}

console.log(`Generated ${Object.keys(art).length} minimalist achievement SVGs in ${root}`);
