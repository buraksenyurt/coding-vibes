// Small Turkish-locale formatters shared by panels and tooltips.

const relative = new Intl.RelativeTimeFormat("tr", { numeric: "auto" });
const absolute = new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium", timeStyle: "short" });

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 365 * 86400],
  ["month", 30 * 86400],
  ["week", 7 * 86400],
  ["day", 86400],
  ["hour", 3600],
  ["minute", 60],
];

/** "2 gün önce", "geçen hafta" ... relative to `now` (seconds). */
export function ago(seconds: number, now = Date.now() / 1000): string {
  const diff = seconds - now;
  for (const [unit, size] of UNITS) {
    if (Math.abs(diff) >= size) return relative.format(Math.round(diff / size), unit);
  }
  return "az önce";
}

export const dateTime = (seconds: number): string => absolute.format(new Date(seconds * 1000));
