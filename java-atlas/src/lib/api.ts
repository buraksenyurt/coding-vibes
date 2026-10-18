// Thin, typed wrappers around Tauri commands. Components never call
// `invoke` directly, so every IPC call is visible in this one file.
// Types come from src/lib/bindings, generated from Rust by ts-rs.
import { Channel, invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";

import type { AppError } from "./bindings/AppError";
import type { AtlasViewDto } from "./bindings/AtlasViewDto";
import type { EvidenceDto } from "./bindings/EvidenceDto";
import type { NodeDetailsDto } from "./bindings/NodeDetailsDto";
import type { ProjectSummary } from "./bindings/ProjectSummary";
import type { RecentProject } from "./bindings/RecentProject";
import type { ScanEvent } from "./bindings/ScanEvent";
import type { SearchHitDto } from "./bindings/SearchHitDto";
import type { TreeDto } from "./bindings/TreeDto";
import type { ViewRequest } from "./bindings/ViewRequest";

export interface AppInfo {
  version: string;
  platform: string;
  javaGrammarOk: boolean;
}

export function appInfo(): Promise<AppInfo> {
  return invoke<AppInfo>("app_info");
}

/** Opens the native folder picker. Resolves to `null` when cancelled. */
export async function pickProjectFolder(): Promise<string | null> {
  const picked = await open({ directory: true, multiple: false, title: "Java projesi seç" });
  return typeof picked === "string" ? picked : null;
}

/**
 * Scans a project. `onProgress` is called for every event streamed back
 * through the channel while Rust works on a background thread.
 */
export function openProject(path: string, onProgress: (event: ScanEvent) => void): Promise<ProjectSummary> {
  const channel = new Channel<ScanEvent>();
  channel.onmessage = onProgress;
  return invoke<ProjectSummary>("open_project", { path, onProgress: channel });
}

export function getAtlasView(request: ViewRequest): Promise<AtlasViewDto> {
  return invoke<AtlasViewDto>("get_atlas_view", { request });
}

export function getNodeDetails(key: string): Promise<NodeDetailsDto> {
  return invoke<NodeDetailsDto>("get_node_details", { key });
}

export function getEdgeEvidence(from: string, to: string): Promise<EvidenceDto[]> {
  return invoke<EvidenceDto[]>("get_edge_evidence", { from, to });
}

export function getTree(): Promise<TreeDto> {
  return invoke<TreeDto>("get_tree");
}

export function search(query: string): Promise<SearchHitDto[]> {
  return invoke<SearchHitDto[]>("search", { query });
}

export function recentProjects(): Promise<RecentProject[]> {
  return invoke<RecentProject[]>("recent_projects");
}

export function forgetRecentProject(path: string): Promise<RecentProject[]> {
  return invoke<RecentProject[]>("forget_recent_project", { path });
}

/** Commands reject with an `AppError`; anything else is a bug or a plugin error. */
export function errorMessage(e: unknown): string {
  if (e && typeof e === "object" && "message" in e) {
    return String((e as AppError).message);
  }
  return String(e);
}

export function isAppError(e: unknown): e is AppError {
  return !!e && typeof e === "object" && "kind" in e && "message" in e;
}
