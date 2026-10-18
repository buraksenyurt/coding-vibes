// Application state for the open project, shared by every component.
// A class with `$state` fields: reading a field inside a component makes
// that component update when the field changes (Svelte 5 runes).
import * as api from "./api";
import type { AtlasViewDto } from "./bindings/AtlasViewDto";
import type { EvidenceDto } from "./bindings/EvidenceDto";
import type { NodeDetailsDto } from "./bindings/NodeDetailsDto";
import type { TreeDto } from "./bindings/TreeDto";
import type { ViewEdgeDto } from "./bindings/ViewEdgeDto";
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

function crumbOf(node: NodeDto): Crumb {
  return { scale: node.kind === "module" ? "region" : "city", key: node.key, name: node.name, rect: node.rect };
}

/** What the details panel shows. */
export type Selection =
  | { kind: "node"; details: NodeDetailsDto }
  | { kind: "road"; edge: ViewEdgeDto; from: NodeDto; to: NodeDto; evidence: EvidenceDto[] };

/** A request for the map's camera; `seq` makes repeated requests distinct. */
export interface Flight {
  rect: RectDto;
  margin: number;
  seq: number;
}

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

  /** The side panel next to the map; hidden to give the map the whole window. */
  showSide = $state(true);
  sideTab = $state<"atlas" | "report">("atlas");
  tree = $state<TreeDto | null>(null);
  selection = $state<Selection | null>(null);
  flight = $state<Flight | null>(null);
  private flightSeq = 0;

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
    if (path !== this.path) {
      this.crumbs = [COUNTRY];
      this.selection = null;
    }
    this.path = path;
    try {
      this.summary = await api.openProject(path, (event) => (this.progress = event));
      this.tree = await api.getTree();
      await this.refreshView();
      await this.refreshSelection();
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
    if (node.kind !== "module" && node.kind !== "package") return;
    const crumbs: Crumb[] = [COUNTRY];
    if (node.kind === "package" && node.parent) {
      const region = this.country?.members.find((m) => m.key === node.parent);
      if (region) crumbs.push(crumbOf(region));
    }
    crumbs.push(crumbOf(node));
    this.crumbs = crumbs;
    await this.loadView();
  }

  /** Asks the map to fly the camera to `rect`. */
  fly(rect: RectDto | null, margin = 0.04) {
    if (rect) this.flight = { rect, margin, seq: ++this.flightSeq };
  }

  /** Shows a node's details without moving the map. */
  async select(key: string) {
    try {
      this.selection = { kind: "node", details: await api.getNodeDetails(key) };
    } catch (e) {
      this.error = api.errorMessage(e);
    }
  }

  async selectRoad(edge: ViewEdgeDto, from: NodeDto, to: NodeDto) {
    const evidence = await api.getEdgeEvidence(edge.from, edge.to).catch(() => []);
    this.selection = { kind: "road", edge, from, to, evidence };
  }

  clearSelection() {
    this.selection = null;
  }

  /**
   * Shows a node wherever it is: picks the scale where it is drawn, loads
   * that view, selects the node and flies the camera there. Used by the
   * tree, the search box, the report and the details panel's links.
   */
  async goToNode(key: string) {
    let details: NodeDetailsDto;
    try {
      details = await api.getNodeDetails(key);
    } catch (e) {
      this.error = api.errorMessage(e);
      return;
    }
    const node = details.node;
    const [module, pkg] = details.path;
    let target = node.rect;
    let margin = 0.04;
    switch (node.kind) {
      case "module":
        this.crumbs = [COUNTRY];
        margin = 0.6; // the region and its neighbours
        break;
      case "package":
        this.crumbs = [COUNTRY, crumbOf(module)];
        margin = 0.6;
        break;
      case "type":
        this.crumbs = [COUNTRY, crumbOf(module), crumbOf(pkg)];
        target = pkg.rect; // the whole city, the building highlighted inside
        break;
      default:
        // Islands and ports live in the base map; the mainland needs the JDK filter.
        if (node.kind === "jdk" && !this.filters.jdk) this.filters = { ...this.filters, jdk: true };
        this.crumbs = [COUNTRY];
        margin = 3;
    }
    await this.refreshView();
    this.selection = { kind: "node", details };
    this.fly(target, margin);
  }

  /** After a rescan, refresh what the details panel shows (or drop it). */
  private async refreshSelection() {
    const sel = this.selection;
    if (sel?.kind !== "node") {
      this.selection = null;
      return;
    }
    try {
      this.selection = { kind: "node", details: await api.getNodeDetails(sel.details.node.key) };
    } catch {
      this.selection = null;
    }
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
