// site/components.js — helpers JS compartilhados, sem deps externas

/**
 * Ativa sistema de tabs dentro de um container.
 * Uso: initTabs(document.querySelector('.tabs-container'))
 * Estrutura esperada:
 *   <div class="tabs-container">
 *     <div class="tabs">
 *       <button class="tab-btn active" data-tab="id1">Label</button>
 *       <button class="tab-btn"        data-tab="id2">Label</button>
 *     </div>
 *     <div class="tab-panel active" id="id1">...</div>
 *     <div class="tab-panel"        id="id2">...</div>
 *   </div>
 */
function initTabs(container) {
  const buttons = container.querySelectorAll('.tab-btn');
  const panels  = container.querySelectorAll('.tab-panel');

  buttons.forEach(btn => {
    btn.addEventListener('click', () => {
      const target = btn.dataset.tab;
      buttons.forEach(b => b.classList.toggle('active', b === btn));
      panels.forEach(p  => p.classList.toggle('active', p.id === target));
    });
  });
}

// Inicializa todos os containers de tabs na página automaticamente.
document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('.tabs-container').forEach(initTabs);
});
