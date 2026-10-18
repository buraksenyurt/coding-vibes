// Application state for the open project, shared by every component.
// A class with `$state` fields: reading a field inside a component makes
// that component update when the field changes (Svelte 5 runes).
import * as api from "./api";
import type { AtlasViewDto } from "./bindings/AtlasViewDto";
import type { NodeDto } from "./bindings/NodeDto";
import type { RectDto } from "./bindings/RectDto";
import type { ScaleDto } from "./bindings/ScaleDto";
import type { ProjectSummary } from "./bindings/ProjectSummary";
import type { RecentProject } from "./bindings/RecentProject";
import type { ScanEvent } from "./bindings/ScanEvent";
import type { ViewFiltersDto } from "./bindings/ViewFiltersDto";

export type Status = "idle" | "scanning" | "ready" | "error";

/** One step of the breadcrumb: Country ▸ region ▸ city. */
export interface Crumb {
  scale: ScaleDto;
  /** The module (region) or package (city) in focus; `null` for the country. */
  key: string | null;
  name: string;
  rect: RectDto | null;
}

const COUNTRY: Crumb = { scale: "country", key: null, name: "Ülke", rect: null };

class ProjectStore {
  status = $state<Status>("idle");
  /** The latest progress event while scanning. */
  progress = $state<ScanEvent | null>(null);
  summary = $state<ProjectSummary | null>(null);
  /** The country view: the base map, always drawn. */
  country = $state<AtlasViewDto | null>(null);
  /** The view of the current scale (the country view itself at country scale). */
  view = $state<AtlasViewDto | null>(null);
  /** Where we are: always starts with the country. */
  crumbs = $state<Crumb[]>([COUNTRY]);
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
    if (path !== this.path) this.crumbs = [COUNTRY];
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

  get scale(): ScaleDto {
    return this.crumbs[this.crumbs.length - 1].scale;
  }

  get focus(): Crumb {
    return this.crumbs[this.crumbs.length - 1];
  }

  /** Zooms into a region (module) or a city (package). */
  async enter(node: NodeDto) {
    const scale: ScaleDto | null = node.kind === "module" ? "region" : node.kind === "package" ? "city" : null;
    if (!scale) return;
    // Entering a region from a city of another region: rebuild the path.
    const base = this.crumbs.filter((c) => c.scale === "country" || (scale === "city" && c.scale === "region"));
    const crumb = { scale, key: node.key, name: node.name, rect: node.rect };
    if (scale === "city" && base.length === 1 && node.parent) {
      const region = this.country?.members.find((m) => m.key === node.parent);
      if (region) base.push({ scale: "region", key: region.key, name: region.name, rect: region.rect });
    }
    this.crumbs = [...base, crumb];
    await this.loadView();
  }

  /** Goes back to the crumb at `index` (0 = country). */
  async goTo(index: number) {
    if (index >= this.crumbs.length - 1) return;
    this.crumbs = this.crumbs.slice(0, index + 1);
    await this.loadView();
  }

  async up() {
    await this.goTo(this.crumbs.length - 2);
  }

  async refreshView() {
    if (!this.summary) return;
    this.country = await api.getAtlasView({ scale: "country", focus: null, filters: this.filters });
    await this.loadView();
  }

  private async loadView() {
    const focus = this.focus;
    if (focus.scale === "country") {
      this.view = this.country;
      return;
    }
    try {
      this.view = await api.getAtlasView({ scale: focus.scale, focus: focus.key, filters: this.filters });
    } catch (e) {
      // After a rescan the focused module or package may be gone.
      if (api.isAppError(e) && e.kind === "unknownNode") {
        this.crumbs = [COUNTRY];
        this.view = this.country;
      } else {
        throw e;
      }
    }
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
