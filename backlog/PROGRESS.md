# Progresso de Implementação — Artificer

Última revisão: 2026-06-13 (sessão 10)

---

## Histórico Completo

| História | Versão |
|---|---|
| P1–P8 — Qualidade, barras, cache, warnings | ≤ v2.10.0.0 |
| Mostrar gasto de armadura | v2.10.1.0 |
| Melhorias de qualidade M1–M7 | v2.10.1.0 |
| Distribuição via repositório Dalamud no GitHub | v2.10.4.0 |
| CosmicTracker: janela flutuante, toggle, modo compacto | v2.10.5.9 |
| 🐛 Fix: ObjectDisposedException RecipeNote | v2.10.5.9 |
| Reorganização estrutural do projeto | v2.11.0.0 |
| Otimizações técnicas: load rápido, reuso, DI | v2.11.0.0 |
| Remover prefixo "Artificer" dos títulos de janela | v2.11.0.2 |
| Componente reutilizável de Empty State | v2.11.0.2 |
| 🐛 Fix: Solver progress bar plana no MacroEditor | v2.11.0.2 |
| 🐛 Fix: CosmicTracker XP desatualizado ao trocar job | v2.11.0.3 |
| Sync upstream v2.11 (bug fixes + features) | v2.13.0.0 |
| Quality Target % + Next Action Forked solver | v2.13.0.0 |
| Lista de Coleta P0 — fundação de dados | v2.14.0.0 |
| Lista de Coleta P1 — helpers (busca, coleta, mercado) | v2.15.0.0 |
| Lista de Coleta P2 — UI completa | v2.16.0.0 |
| 🐛 Fix: Texto "Support me on Ko-fi!" atribui link ao fork | v2.16.0.1 |
| 🐛 Fix: Empty states ausentes (CosmicTracker, MergeWindow, DetailWindow) | v2.16.1.0 |
| 🐛 Fix: NullReferenceException ao atualizar CosmicTracker no último estágio | v2.16.2.0 |
| Settings: aba Experimental (Listas de Coleta + Gear Wear Tracking) | v2.17.0.0 |
| Settings: reorganizar conteúdo das abas — opções movidas para abas corretas | v2.18.0.0 |
| 🐛 Fix: Ações destrutivas sem confirmação (remover receita + limpar gear wear) | v2.18.1.0 |
| Migração de componentes visuais para Artificer.UI | v2.20.0.0 |
| 🐛 Fix: Separadores ultrapassando container e divisores verticais desnecessários | v2.20.10.0 |
| 🐛 Fix: Botões de ícone em branco (DrawCenteredIcon / AddText 5-arg em PushFont) | v2.20.10.0 |
| Character Hash: pre-fill Suggested Macro + indicador de mismatch no Saved | v2.20.11.0 |
| UIStudio Pages stories (13 stories) + skill /update-studio | v2.20.10.0 |

---

## Pendente

| Item | Status | Bloqueio |
|---|---|---|
| Integração com GatherBuddy | 📝 Rascunho — aguarda IPC upstream | Ver `backlog/integracao-gatherbuddy.md` |
| Importar listas do TeamCraft | 📝 Refinado | Ver `backlog/importar-teamcraft.md` |
| Calcular ping para ajustar wait de macro | 📝 Rascunho | Ver `backlog/calcular-ping-wait-macro.md` |
| Internacionalização (i18n) — en + pt-BR | 📝 Refinado | Ver `backlog/internacionalizacao-i18n.md` |
| 🔴 Bug: Lista de Coleta não atualiza ao adicionar receita | 🔴 Bug confirmado | Ver `backlog/bug-lista-coleta-nao-atualiza-apos-add.md` |
| 🔴 Bug: Conteúdo transborda borda direita dos GroupPanels | 🔴 Bug confirmado | Ver `backlog/bug-lista-coleta-overflow-borda-groupPanel.md` |
| Redesign das abas de Settings | 📝 Refinado | Ver `backlog/redesign-abas-settings.md` |
| Consolidação de componentes UI (refactor) | 📝 Rascunho | Ver `backlog/consolidacao-componentes-ui.md` |
