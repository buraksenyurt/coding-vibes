// View preferences. Remembered per machine with localStorage: losing them is
// harmless, so every access is wrapped and falls back to defaults.

export type ColorMode = "kind" | "lane";

const KEY = "easy-git.view";

interface Saved {
  colorMode: ColorMode;
  compact: boolean;
}

function load(): Saved {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) return { colorMode: "kind", compact: false, ...JSON.parse(raw) };
  } catch {
    /* private mode or blocked storage */
  }
  return { colorMode: "kind", compact: false };
}

class ViewState {
  colorMode = $state<ColorMode>(load().colorMode);
  compact = $state<boolean>(load().compact);

  save() {
    try {
      localStorage.setItem(KEY, JSON.stringify({ colorMode: this.colorMode, compact: this.compact }));
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
}

export const view = new ViewState();
