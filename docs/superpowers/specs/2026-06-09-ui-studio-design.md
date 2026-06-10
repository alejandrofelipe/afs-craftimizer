# UI Studio — Design Spec
**Data:** 2026-06-09  
**Status:** Aprovado

## Objetivo

Criar um harness standalone (sem FFXIV, sem Dalamud) que permita visualizar e interagir com os componentes de UI do Craftimizer antes de testar in-game. O harness segue o modelo Storybook: sidebar com navegação por componente, painel de preview ao vivo, painel de controls interativos.

---

## Arquitetura de projetos

Três projetos. A `Craftimizer.UI` é a fonte da verdade de todos os componentes visuais; plugin e Studio a consomem independentemente.

### `Craftimizer.UI.csproj` (novo — biblioteca compartilhada)

- **Não referencia Dalamud.** Depende apenas de `ImGui.NET` e `System.Numerics`.
- Recebe todos os arquivos `Utils/UI/*.cs` movidos do projeto principal:
  - `Colors.cs`, `Theme.cs`, `UIConstants.cs`
  - `ImGuiUtils.cs`, `ImGuiUtils.Progress.cs`, `ImGuiUtils.EmptyState.cs`
  - `ImGuiUtils.Charts.cs`, `ImGuiUtils.SearchableCombo.cs`, `ImGuiUtils.Cosmic.cs`
  - `ProgressBarComponent.cs`, `DynamicBars.cs`
  - `ImGuiExtras.cs`, `ImRaii2.cs`, `SqText.cs`
- Define `IUiServices` (ver abaixo).
- O enum `Configuration.ProgressBarType` é movido para cá como `Craftimizer.UI.ProgressBarType`.

### `Craftimizer.csproj` (existente — plugin Dalamud)

- Adiciona referência ao projeto `Craftimizer.UI`.
- Implementa `IUiServices` com serviços Dalamud reais via `DalamudUiServices : IUiServices`.
- Atribui a instância de `DalamudUiServices` à propriedade estática `UiServices.Current` durante `Plugin.Initialize()`.

### `Craftimizer.UIStudio.csproj` (novo — app standalone)

- App desktop .NET, `OutputType=WinExe`, `TargetFramework=net10.0-windows`.
- Referencia `Craftimizer.UI`.
- Implementa `IUiServices` com stubs simples via `StubUiServices` (ver abaixo).
- Usa **Silk.NET** para janela nativa e contexto OpenGL.
- Usa **ImGui.NET** para renderização ImGui.
- Contém todas as stories de componentes.

---

## Interface `IUiServices`

Isola as duas dependências Dalamud que não são puramente ImGui:

```csharp
// Craftimizer.UI/IUiServices.cs
public interface IUiServices
{
    float GlobalScale { get; }
    IDisposable PushIconFont(); // retorna handle using-friendly
}

// Craftimizer.UI/UiServices.cs — singleton estático
public static class UiServices
{
    public static IUiServices Current { get; set; } = null!;
}
```

Componentes de UI acessam via `UiServices.Current.GlobalScale` e `UiServices.Current.PushIconFont()`.

**Plugin:** `DalamudUiServices` retorna `ImGuiHelpers.GlobalScale` e usa `ImRaii.PushFont(UiBuilder.IconFont)`. Atribuído em `Plugin.Initialize()`.  
**Studio:** `StubUiServices` retorna `1.0f` e usa a fonte FontAwesome carregada do TTF embeddado. Atribuído em `StudioApp` antes do primeiro frame.

Tudo mais que vinha de Dalamud é convertido em constante no próprio projeto `Craftimizer.UI`:
- `ImGuiColors.DalamudWhite2` → `new Vector4(0.78f, 0.78f, 0.78f, 1f)` (constante local em `Colors.cs`)
- `FontAwesomeIcon` → enum próprio em `Craftimizer.UI/Icons/FontAwesomeIcon.cs`, com apenas os codepoints usados pelo projeto

---

## Mapeamento de namespaces Dalamud → ImGui.NET

Feito via `GlobalUsings.cs` no projeto `Craftimizer.UI`:

```csharp
// Craftimizer.UI/GlobalUsings.cs
global using ImGuiNET;
global using ImRaii = ImGuiNET.ImRaii;
// substitui: using Dalamud.Bindings.ImGui;
// substitui: using Dalamud.Interface.Utility.Raii;
```

As APIs são idênticas — ambas bindam o mesmo `imgui.h`. `ImGuiCol`, `ImGuiStyleVar`, `ImGuiTreeNodeFlags`, `ImGuiCond`, etc. existem com o mesmo nome em `ImGuiNET`.

---

## FontAwesome no Studio

Dalamud carrega o TTF automaticamente via seu atlas de fontes. No Studio, carregamos manualmente durante o setup do ImGui:

```csharp
// StudioApp.cs — chamado após ImGui.CreateContext()
var io = ImGui.GetIO();
var ranges = new ushort[] { 0xe000, 0xf8ff, 0 }; // range FA Solid
var config = new ImFontConfigPtr(...) { MergeMode = true };
_iconFont = io.Fonts.AddFontFromFileTTF("fa-solid-900.ttf", 13f, config, ranges);
io.Fonts.Build();
```

O arquivo `fa-solid-900.ttf` é embeddado como `EmbeddedResource` no projeto `UIStudio`. `StubUiServices.PushIconFont()` retorna `ImRaii.PushFont(_iconFont)`.

---

## Rendering loop (Silk.NET)

```
Silk.NET.Windowing → cria janela Win32/OpenGL
       ↓
ImGuiController (boilerplate ~200 linhas) → gerencia frame/render/input
       ↓
StudioWindow.Draw() → ImGui normal
       ├── Sidebar (lista de stories)
       └── Preview + Controls panel
```

**NuGets necessários no UIStudio:**
- `Silk.NET.OpenGL`
- `Silk.NET.Windowing`
- `ImGui.NET`

---

## UI Studio — experiência (Storybook)

### Layout

```
┌─ Sidebar (200px) ─────┬─ Preview (flex) ─────────────┬─ Controls (220px) ─┐
│ 🎨 UI Studio          │ [story tabs: default | ...]   │                    │
│                        │                               │ icon: fa-clipboard │
│ ATOMS                  │                               │ title: "..."       │
│  Colors               │   [live ImGui render]         │ subtitle: "..."    │
│  Theme                │                               │ primaryButton: ... │
│  Typography           │                               │                    │
│                        │                               │                    │
│ MOLECULES             │                               │                    │
│  ► EmptyState ←       │                               │                    │
│  ProgressBar          │                               │                    │
│  Charts               │                               │                    │
│  ...                   │                               │                    │
│                        │                               │                    │
│ ORGANISMS             │                               │                    │
│  (em breve)           │                               │                    │
└───────────────────────┴───────────────────────────────┴────────────────────┘
```

### Interface `IStory`

```csharp
public interface IStory
{
    string Name { get; }
    string Category { get; }        // "Atoms" | "Molecules" | "Organisms"
    string[] StoryNames { get; }    // nomes das variações (tabs)
    void DrawControls(int storyIndex);
    void DrawPreview(int storyIndex);
}
```

Stories são descobertas por **reflection** (`Assembly.GetTypes()` filtrando `IStory`) — sidebar montada automaticamente, sem registro manual.

### Stories V1

| Story | Categoria | Variações |
|---|---|---|
| `ColorsStory` | Atoms | palette, semantic, conditions |
| `ThemeStory` | Atoms | dark theme, buttons |
| `EmptyStateStory` | Molecules | default, no-buttons, with-action |
| `ProgressBarStory` | Molecules | horizontal, arc, compact, stacked, aggregated |
| `ChartsStory` | Molecules | solver bars, collectability |
| `DynamicBarsStory` | Molecules | default, all states |

---

## Mudanças nos arquivos existentes

| Arquivo | O que muda |
|---|---|
| `Utils/UI/*.cs` | Move para `Craftimizer.UI/`, substituição de `using Dalamud.Bindings.ImGui` → `global using ImGuiNET` via GlobalUsings |
| `Utils/UI/*.cs` que usam `ImGuiHelpers.GlobalScale` | Substituem por `UiServices.Current.GlobalScale` |
| `Utils/UI/*.cs` que usam `UiBuilder.IconFont` | Substituem por `UiServices.Current.PushIconFont()` |
| `Configuration.cs` | `ProgressBarType` enum é removido (vive em `Craftimizer.UI`) |
| `Craftimizer.csproj` | Adiciona `<ProjectReference>` para `Craftimizer.UI` |
| `Windows/ProgressBarTestWindow.cs` | Pode ser removido após Studio funcionar |

---

## Fora do escopo (V1)

- Windows completas (`MacroEditor`, `CraftingListWindow`, etc.) — dependem de dados do jogo
- Hot-reload automático de assemblies
- Screenshot/export de stories
- Organisms (janelas compostas)
