# Artificer — Guia de Contexto para Claude

Plugin Dalamud FFXIV. Fork mantido por alejandrofelipe.  
Para contexto completo do projeto: `.claude/agents/artificer-specialist.md`

---

## Onde criar componentes de UI

| O que é | Onde vai |
|---|---|
| Componente ImGui reutilizável **sem** deps Dalamud | `Artificer.UI/ImGuiUtils.<Domínio>.cs` |
| Extension ou utilitário **específico do plugin** (usa Dalamud APIs) | `Artificer/Utils/UI/PluginImGuiUtils.<Domínio>.cs` |
| Story para testar visualmente sem FFXIV | `Artificer.UIStudio/Stories/` |

**Regra rápida:** se o arquivo precisar de `using Dalamud.*`, vai em `Artificer/Utils/UI/`. Caso contrário, vai em `Artificer.UI/`.

---

## Componentes existentes em `Artificer.UI/`

| Componente / Método | Arquivo | Quando usar |
|---|---|---|
| `ImGuiUtils.Tooltip(text, wrapWidth?)` | `ImGuiUtils.cs` | Tooltip sem condição |
| `ImGuiUtils.HoveredTooltip(text, flags?, wrapWidth?)` | `ImGuiUtils.cs` | `IsItemHovered + Tooltip` em uma linha |
| `ImGuiUtils.TooltipWrapped(text, width=300)` | `ImGuiUtils.cs` | Alias de `Tooltip(text, width)` — prefer `HoveredTooltip` com wrapWidth |
| `ImGuiUtils.IconButtonSquare(icon, size?)` | `ImGuiUtils.cs` | Botão de ícone quadrado |
| `ImGuiUtils.IconButtonWithTooltip(icon, tooltip, size?, flags?)` | `ImGuiUtils.cs` | `IconButtonSquare + HoveredTooltip` em uma linha |
| `ImGuiUtils.DrawStateChip(state, label?)` | `ImGuiUtils.Progress.cs` | Dot + label para estado do solver |
| `ImGuiUtils.DrawBadgePill(text, color)` | `ImGuiUtils.Progress.cs` | Badge pill colorida |
| `ImGuiUtils.DrawBadge(handle, size, tooltip)` | `ImGuiUtils.Progress.cs` | Ícone de textura com tooltip no hover |
| `ImGuiUtils.DrawResearchTypeRow(...)` | `ImGuiUtils.Cosmic.cs` | Linha de research do CosmicTracker |
| `ImGuiUtils.DrawSectionHeader(label, rightContent?)` | `ImGuiUtils.cs` | Separator + label colorido + ação opcional |
| `ImGuiUtils.DrawStatRow(label, value, color?, width?)` | `ImGuiUtils.cs` | Label + valor right-aligned |
| `ImGuiUtils.DrawEmptyState(...)` | `ImGuiUtils.EmptyState.cs` | Estado vazio com título, subtítulo, botões |
| `ProgressBarComponent.DrawSingle(...)` | `ProgressBarComponent.cs` | Barra de progresso standalone |
| `ProgressBarComponent.DrawAggregated(...)` | `ProgressBarComponent.cs` | Barra agregando vários snapshots |
| `ImRaii2.GroupPanel(name, width, out avail)` | `ImRaii2.cs` | GroupPanel com borda e label (RAII) |

## Componentes em `Artificer/Utils/UI/` (plugin-side)

| Método | Arquivo | Quando usar |
|---|---|---|
| `PluginImGuiUtils.DrawSolverProgressArea(width, snapshots, type)` | `PluginImGuiUtils.MacroProgress.cs` | Área de progresso do solver (chip + dots + algo + barra) |
| `PluginImGuiUtils.DrawSolverStageDots(snapshot)` | `PluginImGuiUtils.MacroProgress.cs` | Stage dots animados isolados |
| `PluginImGuiUtils.DrawConditionIndicator(condition, spacing)` | (ver arquivo) | Indicador de condição de craft |

---

## Padrões de código obrigatórios

- **RAII sempre** — `ImRaii.PushColor`, `ImRaii.PushStyle`, `ImRaii.Group`, `ImRaii2.GroupPanel`
- **Sem `ImGui.PushStyleColor` / `PopStyleColor` avulso** — sempre usar `using var _ = ImRaii.PushColor(...)`
- **Partial classes por domínio** — `ImGuiUtils.Progress.cs`, `ImGuiUtils.Cosmic.cs`, etc.
- **Cross-assembly overloads** — quando o método aceita `FontAwesomeIcon`, adicionar overload `int` (Dalamud expõe como int em outros projetos)
- **0 build warnings** — sempre

---

## Comandos de build

```powershell
# Build debug (deploy local automático se -Deploy)
.\scripts\build.ps1
.\scripts\build.ps1 -Deploy

# Testes (211 testes)
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test

# UIStudio (visualizar componentes sem FFXIV)
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio

# Version bump
.\scripts\bump-version.ps1 -Type patch   # fix
.\scripts\bump-version.ps1 -Type minor   # feat
.\scripts\bump-version.ps1 -Type build   # chore/refactor
```

> dotnet via Scoop — **sempre usar caminho completo** ou via PowerShell (nunca Bash).
