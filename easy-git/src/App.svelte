<script lang="ts">
  const productName = "easy-git";
</script>

<main class="empty">
  <h1>{productName}</h1>
  <p>Dalların metro haritası burada görünecek.</p>
</main>

<style>
  .empty {
    height: 100%;
    display: grid;
    place-content: center;
    text-align: center;
    color: var(--muted);
  }
  h1 { color: var(--text); margin: 0 0 8px; font-weight: 600; }
</style>
