import { copyFile, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const assets = [
  ["node_modules/fullcalendar/all/global.js", "wwwroot/lib/fullcalendar/all.global.js"],
  ["node_modules/fullcalendar/skeleton.css", "wwwroot/lib/fullcalendar/skeleton.css"],
  ["node_modules/fullcalendar/LICENSE.md", "wwwroot/lib/fullcalendar/LICENSE.md"]
];

for (const [source, destination] of assets) {
  const target = resolve(root, destination);
  await mkdir(dirname(target), { recursive: true });
  await copyFile(resolve(root, source), target);
}
