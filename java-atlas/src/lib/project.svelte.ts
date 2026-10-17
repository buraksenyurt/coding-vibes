// Application state for the open project, shared by every component.
// A class with `$state` fields: reading a field inside a component makes
// that component update when the field changes (Svelte 5 runes).
import * as api from "./api";
import type { AtlasViewDto } from "./bindings/AtlasViewDto";
import type { ProjectSummary } from "./bindings/ProjectSummary";
import type { RecentProject } from "./bindings/RecentProject";
import type { ScanEvent } from "./bindings/ScanEvent";
import type { ViewFiltersDto } from "./bindings/ViewFiltersDto";

export type Status = "idle" | "scanning" | "ready" | "error";

class ProjectStore {
  status = $state<Status>("idle");
  /** The latest progress event while scanning. */
  progress = $state<ScanEvent | null>(null);
  summary = $state<ProjectSummary | null>(null);
  /** Phase 4 preview: the country-scale view. Phase 5 draws it as a map. */
  country = $state<AtlasViewDto | null>(null);
  recent = $state<RecentProject[]>([]);
  error = $state<string | null>(null);
  filters = $state<ViewFiltersDto>({ tests: false, jdk: false, external: true });

  /** The report panel next to the map; hidden to give the map the whole window. */
  showReport = $state(true);

  /** Path of the project being scanned or shown. */
  path = $state<string | null>(null);

  async init() {
    try {
      this.recent = await api.recentProjects();
    } catch {
      this.recent = []; // a broken store file is not worth an error
    }
  }

  async choose() {
    const path = await api.pickProjectFolder();
    if (path) await this.open(path);
  }

  async open(path: string) {
    this.status = "scanning";
    this.error = null;
    this.progress = null;
    this.path = path;
    try {
      this.summary = await api.openProject(path, (event) => (this.progress = event));
      await this.refreshView();
      this.status = "ready";
    } catch (e) {
      this.error = api.errorMessage(e);
      this.status = this.summary ? "ready" : "error";
    } finally {
      this.recent = await api.recentProjects().catch(() => this.recent);
    }
  }

  async rescan() {
    if (this.path) await this.open(this.path);
  }

  async forget(path: string) {
    this.recent = await api.forgetRecentProject(path);
  }

  async setFilters(filters: Partial<ViewFiltersDto>) {
    this.filters = { ...this.filters, ...filters };
    await this.refreshView();
  }

  async refreshView() {
    if (!this.summary) return;
    this.country = await api.getAtlasView({ scale: "country", focus: null, filters: this.filters });
  }
}

export const project = new ProjectStore();

/** Human-readable label for a scan stage. */
export function stageLabel(event: ScanEvent | null): string {
  switch (event?.stage) {
    case "readingBuild":
      return "Build dosyaları okunuyor…";
    case "parsingSources":
      return `Java kaynakları ayrıştırılıyor (${event.done}/${event.total})`;
    case "indexingLibraries":
      return "Kütüphane jar'ları aranıyor…";
    case "analysing":
      return "Bağımlılıklar çözümleniyor…";
    default:
      return "Hazırlanıyor…";
  }
}

/** 0..1, for the progress bar. Parsing is by far the longest stage. */
export function stageFraction(event: ScanEvent | null): number {
  switch (event?.stage) {
    case "readingBuild":
      return 0.03;
    case "parsingSources":
      return 0.05 + 0.85 * (event.total === 0 ? 1 : event.done / event.total);
    case "indexingLibraries":
      return 0.92;
    case "analysing":
      return 0.97;
    default:
      return 0;
  }
}
