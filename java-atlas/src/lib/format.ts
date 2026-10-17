/** 12345 → "12.345" (Turkish grouping). */
export function count(n: number): string {
  return n.toLocaleString("tr-TR");
}

export function duration(ms: number): string {
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toLocaleString("tr-TR", { maximumFractionDigits: 1 })} sn`;
}

export function instability(i: number | null | undefined): string {
  return i == null ? "–" : i.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** The last segment of a path, with either separator. */
export function baseName(path: string): string {
  const parts = path.split(/[\\/]/).filter(Boolean);
  return parts[parts.length - 1] ?? path;
}
