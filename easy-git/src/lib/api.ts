// The only place that talks to Rust. Components call these functions and never
// `invoke` directly, so the IPC surface stays visible in one file.
//
// Outside Tauri (plain `npm run dev` in a browser) the functions fall back to
// a bundled demo (map, branch stats, commit details) built from the
// sample repository. That keeps UI work
// possible without starting the desktop shell.

import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import type { AppError } from "./bindings/AppError";
import type { BranchStatsDto } from "./bindings/BranchStatsDto";
import type { CommitDetailsDto } from "./bindings/CommitDetailsDto";
import type { DemoBundle } from "./bindings/DemoBundle";
import type { ErrorKind } from "./bindings/ErrorKind";
import type { MetroMapDto } from "./bindings/MetroMapDto";
import type { RecentRepository } from "./bindings/RecentRepository";
import type { RepoSummary } from "./bindings/RepoSummary";

export const inTauri = (): boolean => "__TAURI_INTERNALS__" in window;

const DEMO_PATH = "demo://metro-line-story";

/** An `Error` that remembers which `ErrorKind` Rust reported. */
export class ApiError extends Error {
  constructor(
    public readonly kind: ErrorKind | "unknown",
    message: string,
  ) {
    super(message);
  }
}

/** Rust errors arrive as plain `{ kind, message }` objects; turn them into `ApiError`s. */
async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  try {
    return await invoke<T>(command, args);
  } catch (raw) {
    const err = raw as Partial<AppError>;
    throw new ApiError(err?.kind ?? "unknown", err?.message ?? String(raw));
  }
}

export async function pickFolder(): Promise<string | null> {
  if (!inTauri()) return DEMO_PATH;
  const selected = await open({ directory: true, multiple: false, title: "Bir git repository'si seç" });
  return typeof selected === "string" ? selected : null;
}

/** `remember: false` opens without touching the recent list (used by refresh). */
export async function openRepository(path: string, remember = true): Promise<RepoSummary> {
  if (!inTauri()) {
    const { map } = await demo();
    return { name: "metro-line-story (demo)", path: DEMO_PATH, head: map.head };
  }
  return call<RepoSummary>("open_repository", { path, remember });
}

export async function getMetroMap(limit?: number): Promise<MetroMapDto> {
  if (!inTauri()) return (await demo()).map;
  return call<MetroMapDto>("get_metro_map", { limit });
}

export async function getCommitDetails(id: string): Promise<CommitDetailsDto> {
  if (!inTauri()) {
    const found = (await demo()).details.find((d) => d.id === id);
    if (!found) throw new Error(`commit ${id} is not on the map`);
    return found;
  }
  return call<CommitDetailsDto>("get_commit_details", { id });
}

export async function getBranchStats(): Promise<BranchStatsDto[]> {
  if (!inTauri()) return (await demo()).stats;
  return call<BranchStatsDto[]>("get_branch_stats");
}

export async function recentRepositories(): Promise<RecentRepository[]> {
  if (!inTauri()) return [{ name: "metro-line-story (demo)", path: DEMO_PATH }];
  return call<RecentRepository[]>("recent_repositories");
}

export async function forgetRecentRepository(path: string): Promise<RecentRepository[]> {
  if (!inTauri()) return [];
  return call<RecentRepository[]>("forget_recent_repository", { path });
}

async function demo(): Promise<DemoBundle> {
  const module = await import("./demo/demo-bundle.json");
  return module.default as unknown as DemoBundle;
}
