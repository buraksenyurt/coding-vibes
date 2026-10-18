// Light / dark / follow-the-system. The choice is a per-machine convenience,
// so localStorage is enough; losing it just means "follow the system".
export type Theme = "system" | "light" | "dark";

const KEY = "java-atlas.theme";

export function loadTheme(): Theme {
  try {
    const t = localStorage.getItem(KEY);
    return t === "light" || t === "dark" ? t : "system";
  } catch {
    return "system";
  }
}

export function applyTheme(theme: Theme) {
  const root = document.documentElement;
  if (theme === "system") delete root.dataset.theme;
  else root.dataset.theme = theme;
  try {
    localStorage.setItem(KEY, theme);
  } catch {
    // private mode or blocked storage: the theme still applies for this session
  }
}

export function nextTheme(t: Theme): Theme {
  return t === "system" ? "light" : t === "light" ? "dark" : "system";
}

export const themeLabel: Record<Theme, string> = { system: "Sistem", light: "Açık", dark: "Koyu" };
