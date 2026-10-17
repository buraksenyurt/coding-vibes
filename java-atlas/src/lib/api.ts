// Thin, typed wrappers around Tauri commands. Components never call
// `invoke` directly, so every IPC call is visible in one file.
import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";

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
