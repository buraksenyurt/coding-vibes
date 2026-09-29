// Application state as a class with Svelte 5 runes. Any component that reads
// `repo.map` re-renders when it changes; no stores, no subscriptions.

import * as api from "./api";
import type { MetroMapDto } from "./bindings/MetroMapDto";
import type { RecentRepository } from "./bindings/RecentRepository";
import type { RepoSummary } from "./bindings/RepoSummary";

class RepoState {
  summary = $state<RepoSummary | null>(null);
  map = $state<MetroMapDto | null>(null);
  recent = $state<RecentRepository[]>([]);
  loading = $state(false);
  error = $state<string | null>(null);

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
    this.loading = true;
    this.error = null;
    try {
      this.summary = await api.openRepository(path);
      this.map = await api.getMetroMap();
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
}

export const repo = new RepoState();
