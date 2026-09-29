// Application state as a class with Svelte 5 runes. Any component that reads
// `repo.map` re-renders when it changes; no stores, no subscriptions.

import * as api from "./api";
import type { BranchStatsDto } from "./bindings/BranchStatsDto";
import type { CommitDetailsDto } from "./bindings/CommitDetailsDto";
import type { MetroMapDto } from "./bindings/MetroMapDto";
import type { RecentRepository } from "./bindings/RecentRepository";
import type { RepoSummary } from "./bindings/RepoSummary";

export type Selection = { kind: "commit"; column: number } | { kind: "branch"; name: string } | null;

class RepoState {
  summary = $state<RepoSummary | null>(null);
  map = $state<MetroMapDto | null>(null);
  stats = $state<BranchStatsDto[]>([]);
  recent = $state<RecentRepository[]>([]);
  loading = $state(false);
  error = $state<string | null>(null);

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

  async open(path: string) {
    const samePath = this.summary?.path === path;
    this.loading = true;
    this.error = null;
    try {
      this.summary = await api.openRepository(path);
      this.map = await api.getMetroMap();
      this.stats = await api.getBranchStats();
      if (!samePath) this.hiddenLanes = new Set();
      await this.restoreSelection();
      await this.loadRecent();
    } catch (e) {
      this.error = e instanceof Error ? e.message : String(e);
    } finally {
      this.loading = false;
    }
  }

  async refresh() {
    if (this.summary) await this.open(this.summary.path);
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
