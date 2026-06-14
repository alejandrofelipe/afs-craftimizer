# /update-studio

Mantém as stories de Pages do UIStudio em sincronia com as janelas reais do plugin:
- Cria stories para janelas que ainda não têm uma
- Atualiza stories existentes quando há divergência detectada com a janela real
- Avisa sobre stories sem janela correspondente (não deleta automaticamente)
- Atualiza `Program.cs` com novos registros

**Uso:** `/update-studio`

Sem argumentos — a skill lê o estado atual do código e aplica o diff necessário.

---

## Instruções

### Passo 1 — Inventário

Ler todos os arquivos de janelas:
- `Artificer/Windows/*.cs` (apenas arquivos não-partial, ou o arquivo principal de cada partial class)

Ler todas as stories de Pages:
- `Artificer.UIStudio/Stories/Pages/*.cs`

Montar dois sets:
- `windowSet`: nome de cada classe que estenda `Window` (ex: `MacroEditor`, `SynthHelper`)
- `storySet`: valor do campo `Name =>` de cada story de Pages

**Regra de correspondência:** `MacroEditor` (janela) ↔ story com `Name = "MacroEditor"`.

Para partial classes: usar o arquivo principal (sem sufixo como `.Solver`, `.Character`, etc.).
Lista de arquivos principais (não partial):
- `MacroEditor.cs` → `MacroEditor`
- `Settings.cs` → `Settings`
- Os demais são arquivos únicos.

### Passo 2 — Detectar gaps

Para cada janela em `windowSet`:
1. Existe story em `storySet` com nome correspondente?
   - **Não** → marcar como **CRIAR**
2. Existe story mas a janela mudou? Comparar:
   - Novos campos de estado (ex: novo enum, novo bool de visibilidade)
   - Novos componentes de UI usados na janela que não estão na story
   - Se houver divergência visível → marcar como **ATUALIZAR** com descrição do gap
   - Se story ainda cobre bem a janela → **SEM MUDANÇA**

Para cada story em `storySet`:
1. Existe janela correspondente em `windowSet`?
   - **Não** → marcar como **⚠ SEM JANELA** (avisar, não deletar)

### Passo 3 — Aplicar mudanças

**Para cada janela marcada como CRIAR:**
1. Criar `Artificer.UIStudio\Stories\Pages\<Janela>Story.cs` seguindo o padrão:
   - `Category = "Pages"`, `Name = "<Janela>"`
   - Controles de estado no topo do `Draw()` (combos para estados principais)
   - Layout mockado baseado na janela real (sem Dalamud, sem Lumina, sem FFXIVClientStructs)
2. Adicionar `new <Janela>Story(),` em `Artificer.UIStudio/Program.cs` no bloco `// Pages`
3. Build: `dotnet build Artificer.UIStudio/Artificer.UIStudio.csproj --nologo`

**Para cada story marcada como ATUALIZAR:**
1. Ler o arquivo da story atual
2. Identificar o gap específico (ex: "falta estado 'Cosmic' no combo de tipo")
3. Fazer edição direcionada — não reescrever do zero
4. Build: `dotnet build Artificer.UIStudio/Artificer.UIStudio.csproj --nologo`

**Para stories marcadas como ⚠ SEM JANELA:**
- Apenas reportar — não deletar. O usuário decide.

### Passo 4 — Reportar

Ao final, mostrar resumo:

```
UIStudio atualizado

+ Criadas:     NovaJanelaStory.cs
~ Atualizadas: MacroEditorStory.cs  (novo estado "Cosmic" adicionado ao combo de tipo)
⚠ Sem janela:  OldFeatureStory.cs   — confirme se deve ser removida
  Sem mudança: (9 stories já em sincronia)
```

Se nenhuma mudança necessária:
```
UIStudio já em sincronia com Windows/. Nenhuma alteração feita.
```

---

## O que esta skill NÃO faz

- **Não deleta** stories — apenas avisa sobre as órfãs
- **Não detecta** mudanças puramente visuais (cores, padding) — foca em estado e componentes
- **Não executa** `dotnet run` — a verificação visual é responsabilidade do usuário
