// Application state as a class with Svelte 5 runes. Any component that reads
// `repo.map` re-renders when it changes; no stores, no subscriptions.

import * as api from "./api";
import { ApiError } from "./api";
import type { BranchStatsDto } from "./bindings/BranchStatsDto";
import type { CommitDetailsDto } from "./bindings/CommitDetailsDto";
import type { MetroMapDto } from "./bindings/MetroMapDto";
import type { RecentRepository } from "./bindings/RecentRepository";
import type { RepoSummary } from "./bindings/RepoSummary";

export interface Notice {
  tone: "info" | "error";
  text: string;
}

export type Selection = { kind: "commit"; column: number } | { kind: "branch"; name: string } | null;

class RepoState {
  summary = $state<RepoSummary | null>(null);
  map = $state<MetroMapDto | null>(null);
  stats = $state<BranchStatsDto[]>([]);
  recent = $state<RecentRepository[]>([]);
  loading = $state(false);
  error = $state<string | null>(null);
  /** A short message shown above the map; unlike `error` it keeps the map on screen. */
  notice = $state<Notice | null>(null);

  selection = $state<Selection>(null);
  details = $state<CommitDetailsDto | null>(null);
  /** Lanes the user switched off in the side bar. */
  hiddenLanes = $state<Set<number>>(new Set());
  /** Column the map should scroll into view next; the map clears it. */
  scrollTarget = $state<number | null>(null);

  async loadRecent() {
    try {
      this.recent = await api.recentRepositories();
    } catch {
      this.recent = [];
    }
  }

  async pickAndOpen() {
    const path = await api.pickFolder();
    if (path) await this.open(path);
  }

  /** Opens an entry of the "recently opened" menu. */
  async openRecent(entry: RecentRepository) {
    await this.open(entry.path, entry.name);
  }

  /**
   * `recentName` is set when the path came from the recent list.
   * `remember` is false for a refresh: re-reading the open repository should
   * not put it back into a recent list the user just trimmed.
   */
  async open(path: string, recentName?: string, remember = true) {
    const samePath = this.summary?.path === path;
    this.loading = true;
    this.error = null;
    this.notice = null;
    try {
      this.summary = await api.openRepository(path, remember);
      this.map = await api.getMetroMap();
      this.stats = await api.getBranchStats();
      if (!samePath) this.hiddenLanes = new Set();
      await this.restoreSelection();
      await this.loadRecent();
    } catch (e) {
      this.reportOpenFailure(e, path, recentName);
    } finally {
      this.loading = false;
    }
  }

  private async reportOpenFailure(e: unknown, path: string, recentName?: string) {
    const kind = e instanceof ApiError ? e.kind : "unknown";
    const message = e instanceof Error ? e.message : String(e);
    const gone = kind === "notFound" || kind === "notARepository";

    if (gone && recentName !== undefined) {
      // Rust has already dropped the entry; refresh the menu and say why.
      const reason = kind === "notFound"
        ? "klasör bulunamadı (taşınmış, adı değişmiş ya da silinmiş olabilir)"
        : "klasör artık bir git reposu değil";
      this.notice = {
        tone: "info",
        text: `“${recentName}” açılamadı: ${reason}. Son kullanılanlar listesinden çıkarıldı.`,
      };
      await this.loadRecent();
      return;
    }
    const text = kind === "notFound" ? `Klasör bulunamadı: ${path}`
      : kind === "notARepository" ? `Seçilen klasör bir git reposu değil: ${path}`
      : message;
    // With a map on screen, keep it and show a notice instead of the error page.
    if (this.map) this.notice = { tone: "error", text };
    else this.error = text;
  }

  dismissNotice() {
    this.notice = null;
  }

  async refresh() {
    if (this.summary) await this.open(this.summary.path, undefined, false);
  }

  /** Removes an entry from the recent list; the repository itself is untouched. */
  async forgetRecent(entry: RecentRepository) {
    try {
      this.recent = await api.forgetRecentRepository(entry.path);
      this.notice = { tone: "info", text: `“${entry.name}” son kullanılanlar listesinden çıkarıldı.` };
    } catch (e) {
      this.notice = { tone: "error", text: e instanceof Error ? e.message : String(e) };
    }
  }

  async selectCommit(column: number, scroll = false) {
    if (!this.map) return;
    this.selection = { kind: "commit", column };
    if (scroll) this.scrollTarget = column;
    try {
      this.details = await api.getCommitDetails(this.map.commits[column].id);
    } catch (e) {
      this.details = null;
      this.error = e instanceof Error ? e.message : String(e);
    }
  }

  selectBranch(name: string) {
    const stat = this.stats.find((s) => s.name === name);
    this.selection = { kind: "branch", name };
    this.details = null;
    if (stat) {
      if (stat.lane !== null && this.hiddenLanes.has(stat.lane)) this.toggleLane(stat.lane);
      this.scrollTarget = stat.tipColumn;
    }
  }

  clearSelection() {
    this.selection = null;
    this.details = null;
  }

  toggleLane(lane: number) {
    const next = new Set(this.hiddenLanes);
    if (next.has(lane)) next.delete(lane);
    else next.add(lane);
    this.hiddenLanes = next;
  }

  /** After a refresh the same commit may sit in another column. */
  private async restoreSelection() {
    const sel = this.selection;
    if (sel?.kind === "commit" && this.details) {
      const column = this.map?.commits.findIndex((c) => c.id === this.details!.id) ?? -1;
      if (column >= 0) await this.selectCommit(column);
      else this.clearSelection();
    } else if (sel?.kind === "branch" && !this.stats.some((s) => s.name === sel.name)) {
      this.clearSelection();
    }
  }
}

export const repo = new RepoState();
