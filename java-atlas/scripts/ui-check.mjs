// Drives the front end in Chromium with a fake Tauri bridge, for quick UI
// checks and screenshots without building the desktop app.
//
//   FIXTURE_DIR=$PWD/target/ui-fixtures cargo test -p java-atlas dump_fixtures -- --ignored
//   npm install --no-save playwright@1.56.1 && npx playwright install chromium   # once
//   VITE_AUTO_OPEN=demo npm run dev               # in another terminal; any value: the fake bridge answers
//   node scripts/ui-check.mjs target/ui/shot "wheel:600:330,wait:700,shot:region"
//
// Steps: click:x:y  dbl:x:y  move:x:y  wheel:x:y  key:Name  wait:ms  shot:name
// CHROMIUM=/path/to/chrome overrides the browser Playwright would pick.
import { chromium } from "playwright";
import fs from "node:fs";
const dir = process.env.FIXTURE_DIR ?? "target/ui-fixtures";
const fx = (n) => JSON.parse(fs.readFileSync(`${dir}/${n}.json`, "utf8"));
const fixtures = { summary: fx("summary"), country: fx("country"), region: fx("region-bank-service"), city: fx("city-account") };
const out = process.argv[2] ?? "target/ui/shot";
const steps = (process.argv[3] ?? "").split(",").filter(Boolean);
const theme = process.argv[4] ?? "light";

const browser = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
const page = await browser.newPage({ viewport: { width: 1280, height: 800 }, colorScheme: theme });
page.on("console", (m) => { if (m.type() === "error" || m.type() === "warning") console.log("console:", m.type(), m.text()); });
page.on("pageerror", (e) => console.log("pageerror:", e.message));
await page.addInitScript((fixtures) => {
  let cb = 0;
  const callbacks = {};
  window.__TAURI_INTERNALS__ = {
    transformCallback: (fn) => { const id = ++cb; callbacks[id] = fn; return id; },
    unregisterCallback: () => {},
    invoke: async (cmd, args) => {
      switch (cmd) {
        case "app_info": return { version: "0.1.0", platform: "browser", javaGrammarOk: true };
        case "recent_projects": return [];
        case "open_project": return fixtures.summary;
        case "get_atlas_view": {
          const s = args.request.scale;
          if (s === "country") return fixtures.country;
          if (s === "region" && args.request.focus === fixtures.region.focus.key) return fixtures.region;
          if (s === "city" && args.request.focus === fixtures.city.focus.key) return fixtures.city;
          throw { kind: "internal", message: "no fixture for " + JSON.stringify(args.request) };
        }
        default: throw { kind: "internal", message: "not mocked: " + cmd };
      }
    },
  };
}, fixtures);
await page.goto("http://localhost:1420/");
await page.waitForTimeout(1500);
for (const step of steps) {
  const [what, a, b] = step.split(":");
  if (what === "click") await page.mouse.click(+a, +b);
  if (what === "dbl") { await page.mouse.click(+a, +b); await page.waitForTimeout(100); await page.mouse.click(+a, +b); }
  if (what === "move") await page.mouse.move(+a, +b);
  if (what === "wheel") { await page.mouse.move(+a, +b); for (let i = 0; i < 6; i++) await page.mouse.wheel(0, -120); }
  if (what === "key") await page.keyboard.press(a);
  if (what === "wait") await page.waitForTimeout(+a);
  if (what === "shot") await page.screenshot({ path: `${out}-${a}.png` });
}
await page.screenshot({ path: `${out}.png` });
await browser.close();
