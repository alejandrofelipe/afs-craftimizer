---
name: window-state-visual
description: >
  Automatiza a criação de documentação visual de estados para uma janela do plugin Artificer.
  Gera a UIStudio Story expandida e o HTML page em `site/`.
  Usar quando adicionar ou atualizar documentação visual de uma janela.
---

# Window State Visual

Cria ou atualiza a documentação visual de estados de uma janela do Artificer.

## Entradas esperadas

O usuário deve informar:
- Nome da janela (ex: `SynthesisHelper`)
- Arquivo principal da janela (ex: `Artificer/Windows/SynthesisHelper.cs`)

## Processo

### 1. Mapear estados

Ler o arquivo da janela e identificar:
- Todos os estados/enums que controlam a renderização
- Cada bloco `if/else/switch` significativo no `Draw()`
- Componentes condicionais (opcional vs sempre)

Produzir uma tabela markdown:

| Seção | Sub-estados |
|---|---|
| ... | ... |

### 2. UIStudio Story

**Arquivo:** `Artificer.UIStudio/Stories/Pages/<WindowName>Story.cs`

- Se o arquivo já existe: expandir as seções faltantes
- Se não existe: criar do zero seguindo o padrão abaixo

**Padrão da Story:**
```csharp
internal sealed class <WindowName>Story : IStory
{
    public string Category => "Pages";
    public string Name     => "<WindowName>";

    private static readonly string[] Sections = [ /* uma entrada por seção */ ];
    private int _section;

    public void Draw()
    {
        ImGui.SetNextItemWidth(240f);
        ImGui.Combo("Seção##<wn>", ref _section, Sections, Sections.Length);
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        var width = ImGui.GetContentRegionAvail().X;
        // switch (_section) { case 0: DrawSection_X(width); break; ... }
    }

    // DrawSection_<Nome>(float _) — DrawGallery com sub-estados
    // DrawMockMacroCard / DrawMockArcGrid — copiar de CraftingHelperStory se necessário
}
```

**Regras:**
- UIStudio só referencia `Artificer.UI` — não usar `PluginImGuiUtils`
- Substituir `DrawMacroStatArcs` por grid 2×2 colorido (ver `CraftingHelperStory.DrawMockArcGrid`)
- Substituir `DrawSolverProgressArea` por `DrawStateChip` + `ProgressBar`
- Dados mockados: `const` ou `static readonly` no topo da classe
- Build deve passar com 0 warnings

### 3. HTML page

**Arquivo:** `site/<kebab-window-name>.html`

- Copiar estrutura de `site/crafting-helper.html`
- Substituir: título, tabs, estado-cards
- Cada seção = uma tab; cada sub-estado = um `.state-card`
- Usar classes CSS existentes de `site/style.css` — não adicionar CSS inline além do necessário

### 4. Atualizar index

Adicionar card em `site/index.html`:

```html
<a href="<kebab-name>.html" class="window-card" style="display:block;text-decoration:none;color:inherit;">
  <h3><WindowName></h3>
  <p class="meta">N estados · M seções</p>
</a>
```

### 5. Build + verificação

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer.UIStudio --no-restore -v quiet
```
Abrir `site/<kebab-name>.html` no browser e verificar todas as tabs.

### 6. Commit

```
git add Artificer.UIStudio/Stories/Pages/<WindowName>Story.cs
git add site/<kebab-name>.html site/index.html
git commit -m "feat(studio+site): <WindowName> visual states — N estados em M seções"
```
