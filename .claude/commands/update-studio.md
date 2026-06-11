# /update-studio

Mantém as **Pages stories** do UIStudio (`Artificer.UIStudio/Stories/Pages/`) em sincronia
com as janelas reais do plugin (`Artificer/Windows/`). Detecta janelas sem story, stories
desatualizadas e stories órfãs, e aplica as correções necessárias.

**Uso:** `/update-studio`

Sem argumentos — a skill faz o inventário, detecta lacunas, aplica mudanças e reporta.

---

## O que esta skill faz

1. **Inventaria** as janelas reais do plugin e as Pages stories existentes
2. **Detecta lacunas** entre os dois conjuntos (CRIAR / ATUALIZAR / SEM JANELA)
3. **Aplica** as mudanças — cria stories novas, edita as desatualizadas, registra em `Program.cs`, builda
4. **Reporta** um resumo do que mudou

## O que esta skill NÃO faz

- **Não deleta stories.** Uma story sem janela correspondente é apenas marcada `⚠ SEM JANELA` — o usuário decide se remove.
- **Não reescreve stories que funcionam do zero.** Para uma story desatualizada, faz apenas edição cirúrgica (Edit) do que mudou, nunca um Write completo.
- **Não infere lógica de render complexa automaticamente.** Quando o gap exige render novo e não óbvio, descreve o gap no relatório e implementa o necessário — não tenta adivinhar comportamento que não está claro no código da janela.
- **Não toca em stories de outras categorias.** Apenas `Category = "Pages"`. Stories `Atoms` / `Molecules` / `Templates` (`Artificer.UIStudio/Stories/*.cs` na raiz) ficam intactas.

---

## Passo 1 — Inventário

Ler **antes de qualquer edição**:

- Todos os `Artificer/Windows/*.cs` — focar no arquivo principal de cada janela (o que tem a declaração `class X : Window`), não nas partial classes auxiliares (ex: `MacroEditor.Hotbars.cs`, `Settings.Solver.cs` são partials de `MacroEditor.cs` / `Settings.cs`).
- Todos os `Artificer.UIStudio/Stories/Pages/*.cs`.

Montar dois conjuntos:

- **`windowSet`** — nome de cada subclasse de `Window`. Identificar via:
  ```
  grep "class \w+ ?: ?Window" em Artificer/Windows/*.cs
  ```
  Ex: `MacroEditor`, `SynthHelper`, `RecipeNote`, `CosmicTracker`, `MacroClipboard`, `MacroList`, `Settings`, `CraftingListWindow`, `FeatureHubWindow`, ...
  > Janelas de dev/teste (ex: `ProgressBarTestWindow`) podem não ter story — tratar como gap normal e reportar, não criar story automaticamente para janela claramente de teste a menos que o usuário peça.

- **`storySet`** — valor de `Name =>` de cada story em `Pages/`. Ex: a story com `public string Name => "RecipeNote";` entra como `RecipeNote`.

**Regra de matching:** janela `MacroEditor` ↔ story com `Name = "MacroEditor"` (igualdade exata do nome, sem o sufixo `Story` do arquivo).

---

## Passo 2 — Detectar lacunas

Comparar `windowSet` e `storySet`. Três situações:

| Situação | Marcação | Ação no Passo 3 |
|---|---|---|
| Janela **sem** story correspondente | **CRIAR** | Criar story + registrar + buildar |
| Story existe, mas a janela real ganhou estados/componentes novos | **ATUALIZAR** | Edit cirúrgico + buildar |
| Story **sem** janela correspondente | **⚠ SEM JANELA** | Apenas avisar — nunca deletar |

Para **ATUALIZAR**, comparar o conteúdo da story com a janela real e anotar o **diff específico**
(ex: "janela `MacroEditor` agora tem aba Cosmic que a story não cobre"; "novo `CraftableStatus`
em `RecipeNote.cs` ausente no array `CraftableStatuses` da story"). Se a story já cobre todos os
estados/componentes relevantes da janela, marcar como **sem mudança**.

---

## Passo 3 — Aplicar mudanças

### Para cada CRIAR

Criar `Artificer.UIStudio/Stories/Pages/<Name>Story.cs` seguindo o padrão das stories existentes
(`RecipeNoteStory.cs`, `MacroEditorStory.cs` são bons modelos):

- `namespace Artificer.UIStudio.Stories;`
- `internal sealed class <Name>Story : IStory`
- `public string Category => "Pages";`
- `public string Name => "<Name>";` — **igual ao nome da janela**
- Combos de estado no topo (`DrawControls`) para alternar entre os estados que a janela tem
  (ex: vazio / carregando / pronto; tipos de receita; status de craftabilidade)
- Layout **mockado** do conteúdo da janela usando os componentes de `Artificer.UI`
  (`ImRaii2.GroupPanel`, `ImGuiUtils.Draw*`, `Theme.Push*`, `Colors.*`) — **sem** dependências Dalamud,
  **sem** acesso a Lumina/FFXIVClientStructs. Dados são mock estático (vide `RecipeName(...)`, `ProgressMax`, etc.).

Registrar a instância em `Artificer.UIStudio/Program.cs`, na seção `// Pages` da lista passada
para `StudioApp.Run([...])`:

```csharp
    // Pages
    new MacroEditorStory(),
    ...
    new <Name>Story(),   // ← adicionar aqui
```

Buildar para validar (ver Passo de verificação).

### Para cada ATUALIZAR

Fazer **Edit cirúrgico** no arquivo da story — alterar apenas o que o diff aponta
(adicionar um valor a um array de estados, um novo branch no `switch`, uma nova barra/badge).
**Nunca** reescrever o arquivo inteiro com Write. Buildar para validar.

### Para ⚠ SEM JANELA

Nada a fazer no código. Apenas registrar no relatório para o usuário confirmar remoção.

---

## Passo de verificação (build)

Após criar/editar stories, validar que o UIStudio ainda compila:

```powershell
dotnet build Artificer.UIStudio/Artificer.UIStudio.csproj --nologo
```

> Se `dotnet` não for encontrado:
> `$env:PATH = "C:\Users\aleja\scoop\apps\dotnet-sdk\current;$env:PATH"`

Se o build quebrar, corrigir antes de reportar.

---

## Passo 4 — Reportar ao usuário

```
UIStudio atualizado

+ Criadas:     NovaJanelaStory.cs
~ Atualizadas: MacroEditorStory.cs  (novo estado "Cosmic" adicionado)
⚠ Sem janela:  OldFeatureStory.cs   — confirme se deve ser removida
  Sem mudança: (9 stories)
```

Se nada precisou ser feito:

```
UIStudio já em sincronia. Nenhuma alteração feita.
```

---

## Referências

- Janelas reais (fonte de verdade): `Artificer/Windows/*.cs`
- Stories de Pages: `Artificer.UIStudio/Stories/Pages/*.cs`
- Registro de stories: `Artificer.UIStudio/Program.cs`
- Interface: `Artificer.UIStudio/IStory.cs` (`Category`, `Name`, `Draw`)
- Modelos de story: `Stories/Pages/RecipeNoteStory.cs`, `Stories/Pages/MacroEditorStory.cs`
- Componentes UI disponíveis (sem Dalamud): `Artificer.UI/` (`ImGuiUtils*`, `ProgressBarComponent`, `Theme`, `Colors`, `ImRaii2`)
