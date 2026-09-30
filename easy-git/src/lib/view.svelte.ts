// View preferences. Remembered per machine with localStorage: losing them is
// harmless, so every access is wrapped and falls back to defaults.

import { getCurrentWindow } from "@tauri-apps/api/window";

export type ColorMode = "kind" | "lane";
/** "system" follows Windows' light/dark setting; the others override it. */
export type Theme = "system" | "light" | "dark";

const KEY = "easy-git.view";

interface Saved {
  colorMode: ColorMode;
  compact: boolean;
  theme: Theme;
}

const DEFAULTS: Saved = { colorMode: "kind", compact: false, theme: "system" };

function load(): Saved {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) return { ...DEFAULTS, ...JSON.parse(raw) };
  } catch {
    /* private mode or blocked storage */
  }
  return DEFAULTS;
}

const saved = load();

class ViewState {
  colorMode = $state<ColorMode>(saved.colorMode);
  compact = $state<boolean>(saved.compact);
  theme = $state<Theme>(saved.theme);

  save() {
    try {
      localStorage.setItem(
        KEY,
        JSON.stringify({ colorMode: this.colorMode, compact: this.compact, theme: this.theme }),
      );
    } catch {
      /* ignore */
    }
  }

  setColorMode(mode: ColorMode) {
    this.colorMode = mode;
    this.save();
  }

  toggleCompact() {
    this.compact = !this.compact;
    this.save();
  }

  setTheme(theme: Theme) {
    this.theme = theme;
    this.save();
    this.applyTheme();
  }

  /**
   * The colours live in app.css as CSS variables. A `data-theme` attribute on
   * <html> picks the light or dark set; without it the OS preference decides
   * (`prefers-color-scheme`). The native title bar is told the same thing.
   */
  applyTheme() {
    const root = document.documentElement;
    if (this.theme === "system") delete root.dataset.theme;
    else root.dataset.theme = this.theme;

    if ("__TAURI_INTERNALS__" in window) {
      getCurrentWindow()
        .setTheme(this.theme === "system" ? null : this.theme)
        .catch(() => {
          /* older WebView or missing permission: the page still switches */
        });
    }
  }
}

export const view = new ViewState();
