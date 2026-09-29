<script lang="ts">
  import type { Snippet } from "svelte";

  // Follows the pointer; flips to the other side near the window edges.
  let { x, y, children }: { x: number; y: number; children: Snippet } = $props();

  let box: HTMLDivElement | undefined = $state();
  const OFFSET = 14;

  const left = $derived(box && x + OFFSET + box.offsetWidth > window.innerWidth ? x - OFFSET - box.offsetWidth : x + OFFSET);
  const top = $derived(box && y + OFFSET + box.offsetHeight > window.innerHeight ? y - OFFSET - box.offsetHeight : y + OFFSET);
</script>

<div class="tooltip" bind:this={box} style:left="{left ?? x + OFFSET}px" style:top="{top ?? y + OFFSET}px" role="tooltip">
  {@render children()}
</div>

<style>
  .tooltip {
    position: fixed;
    z-index: 20;
    max-width: 360px;
    padding: 8px 10px;
    border-radius: 8px;
    background: var(--surface);
    border: 1px solid var(--border);
    box-shadow: 0 6px 20px rgb(0 0 0 / 0.16);
    font-size: 12px;
    color: var(--text);
    pointer-events: none;
  }
</style>
