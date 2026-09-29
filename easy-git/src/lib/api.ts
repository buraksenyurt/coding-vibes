// The only place that talks to Rust. Components call these functions and never
// `invoke` directly, so the IPC surface stays visible in one file.
//
// Outside Tauri (plain `npm run dev` in a browser) the functions fall back to
// a bundled demo map built from the sample repository. That keeps UI work
// possible without starting the desktop shell.

import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import type { AppError } from "./bindings/AppError";
import type { MetroMapDto } from "./bindings/MetroMapDto";
import type { RecentRepository } from "./bindings/RecentRepository";
import type { RepoSummary } from "./bindings/RepoSummary";

export const inTauri = (): boolean => "__TAURI_INTERNALS__" in window;

const DEMO_PATH = "demo://metro-line-story";

/** Rust errors arrive as plain `{ kind, message }` objects; turn them into `Error`s. */
async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  try {
    return await invoke<T>(command, args);
  } catch (raw) {
    const err = raw as Partial<AppError>;
    throw new Error(err?.message ?? String(raw));
  }
}

export async function pickFolder(): Promise<string | null> {
  if (!inTauri()) return DEMO_PATH;
  const selected = await open({ directory: true, multiple: false, title: "Bir git repository'si seç" });
  return typeof selected === "string" ? selected : null;
}

export async function openRepository(path: string): Promise<RepoSummary> {
  if (!inTauri()) {
    const map = await demoMap();
    return { name: "metro-line-story (demo)", path: DEMO_PATH, head: map.head };
  }
  return call<RepoSummary>("open_repository", { path });
}

export async function getMetroMap(limit?: number): Promise<MetroMapDto> {
  if (!inTauri()) return demoMap();
  return call<MetroMapDto>("get_metro_map", { limit });
}

export async function recentRepositories(): Promise<RecentRepository[]> {
  if (!inTauri()) return [{ name: "metro-line-story (demo)", path: DEMO_PATH }];
  return call<RecentRepository[]>("recent_repositories");
}

async function demoMap(): Promise<MetroMapDto> {
  const module = await import("./demo/sample-map.json");
  return module.default as unknown as MetroMapDto;
}
