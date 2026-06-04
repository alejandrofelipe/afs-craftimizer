# /commit

Prepara e executa o commit completo de uma mudança no Craftimizer:
atualiza README, faz version bump, cria commit com mensagem estruturada, cria tag e faz push.

**Uso:** `/commit <descrição curta do que foi feito>`

O argumento `$ARGUMENTS` descreve a mudança — se omitido, inspecionar o `git diff` para inferir.

---

## Instruções

### Passo 1 — Atualizar o README

Executar o fluxo do comando `/update-readme`:
- Avaliar se a mudança merece nova entrada em "Diferenças deste Fork"
  - Sim: adicionar o bullet
  - Não: nenhuma alteração de conteúdo ainda (a versão será atualizada após o bump)

> A linha de versão do README será atualizada **após** o bump, no Passo 3.

### Passo 2 — Fazer o version bump

Executar o fluxo do comando `/version-bump`:
- Determinar o tipo da mudança com base em `$ARGUMENTS` e/ou `git diff`:
  - `feat` → bump minor (X.Y+1.0.0)
  - `fix` / `refactor` / `chore` → bump patch (X.Y.Z.W+1)
- Atualizar `Craftimizer/Craftimizer.csproj` com a nova versão
- Ler a versão resultante como `$VERSION`

Após o bump, atualizar também a linha de versão no README:
```
**Versão atual:** $VERSION · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0
```

### Passo 3 — Determinar tipo do commit e escopo

Com base em `$ARGUMENTS` e/ou `git diff`:

| Mudança | Tipo | Escopo sugerido |
|---|---|---|
| Correção de bug | `fix` | `ui`, `simulator`, `solver`, `hooks`, `config` |
| Nova funcionalidade | `feat` | idem |
| Refactor / limpeza | `refactor` | idem |
| Manutenção / scripts | `chore` | `build`, `config`, `scripts` |
| Documentação | `docs` | — |
| Breaking change | `feat!` ou `fix!` | idem |

### Passo 4 — Montar mensagem de commit

Usar este template:

```
<tipo>(<escopo>): <título em imperativo, max 72 chars>

<corpo — o QUE mudou e POR QUÊ, em bullets se múltiplos itens>

Version: <VERSION>
<Se fechar issue: Fixes #N / Closes #N>

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

**Regras do título:**
- Imperativo: "corrigir", "adicionar", "remover", "atualizar" (não "corrigido", "adicionado")
- Sem ponto final
- Max 72 caracteres
- Em português

**Regras do corpo:**
- Explicar a causa raiz se for um fix, ou a motivação se for uma feature
- Listar arquivos/componentes principais modificados em bullets
- Omitir se a mudança for trivial e o título for autoexplicativo

**Exemplo de mensagem bem formada:**
```
feat(ui): adicionar progresso de Cosmic Tool em tempo real no Crafting Log

Research data da Cosmic Tool agora atualiza automaticamente após entregar
um collectable em Stellar Missions, sem precisar reabrir a janela.

- Adicionado CosmicToolTracker com hooks em WKSManager.Load,
  WKSMissionModule.ReportMission e AbandonMission
- RecipeNote e MacroEditor assinam OnProgressChanged e redesenham ao receber dado novo

Version: 2.10.3.0

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

### Passo 5 — Staging e commit

```powershell
# Staged o que foi modificado (código + README + .csproj)
git add <arquivos alterados>
git add README.md
git add Craftimizer/Craftimizer.csproj

# Commit
git commit -m "<mensagem montada no Passo 4>"
```

### Passo 6 — Criar tag de versão

```powershell
git tag -a "v$VERSION" -m "Release $VERSION - <título curto>"
```

O título curto da tag deve ser o mesmo do commit, sem o prefixo convencional.

**Exemplo:** `git tag -a v2.10.3.0 -m "Release 2.10.3.0 - Cosmic Tool progress em tempo real"`

### Passo 7 — Push do commit e da tag

```powershell
git push origin main
git push origin "v$VERSION"
```

> Usar `git push origin main --tags` apenas se houver múltiplas tags novas para subir de uma vez.

### Passo 8 — Confirmar ao usuário

Reportar:
- Versão commitada: `vX.Y.Z.W`
- Hash do commit
- Tag criada: `vX.Y.Z.W`
- Push realizado para `origin/main`
- O que foi atualizado no README (versão + bullet se aplicável)
- Lembrete se teste in-game for necessário para validar a mudança

---

## Casos especiais

### Sem mudanças staged ou unstaged
Se `git status` não mostrar nada além do que já está no último commit:
1. Informar que não há nada novo para commitar
2. Não criar commit vazio

### Múltiplas mudanças não relacionadas
Se o diff cobrir mudanças de escopos distintos sem relação, alertar o usuário e sugerir commits separados em vez de um commit grande.
