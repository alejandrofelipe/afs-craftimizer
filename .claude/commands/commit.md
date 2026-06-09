# /commit

Prepara e executa o commit completo de uma mudança no Craftimizer:
atualiza README, faz version bump, cria commit com mensagem estruturada, cria tag e faz push.

**Uso:**
```
/commit <descrição curta do que foi feito>
/commit <nível> <descrição curta do que foi feito>
```

**Níveis de bump disponíveis** (mesmo que `bump-version.ps1 -Type`):

| Nível | Efeito | Quando usar |
|---|---|---|
| `major` | X+1.0.0.0 | Breaking change, rewrite, remoção de API |
| `minor` | X.Y+1.0.0 | Nova feature (`feat`) |
| `patch` | X.Y.Z+1.0 | Correção de bug relevante (`fix`) |
| `build` | X.Y.Z.W+1 | Refactor, chore, polish, docs (padrão) |

**Exemplos:**
```
/commit minor adicionar empty state reutilizável
/commit patch corrigir crash no RecipeNote
/commit build ajustar títulos das janelas
/commit major rewrite do solver
/commit remover prefixo das janelas   ← auto-detecta: chore → build
```

Se nenhum nível for fornecido como primeiro token, auto-detectar com base no diff:
- `feat` → `minor`
- `fix` → `patch`
- `refactor` / `chore` / `docs` / `style` → `build`

O argumento `$ARGUMENTS` descreve a mudança — se omitido, inspecionar o `git diff` para inferir.

---

## Instruções

### Passo 1 — Parsear argumentos

Verificar se o primeiro token de `$ARGUMENTS` é um dos níveis válidos: `major`, `minor`, `patch`, `build`.

- **Se sim:** usar esse nível explicitamente; o restante do texto é a descrição.
- **Se não:** a string completa é a descrição; o nível será determinado no Passo 2 via auto-detecção.

### Passo 2 — Atualizar o README

Executar o fluxo do comando `/update-readme`:
- Avaliar se a mudança merece nova entrada em "Diferenças deste Fork"
  - Sim: adicionar o bullet
  - Não: nenhuma alteração de conteúdo ainda (a versão será atualizada após o bump)

> A linha de versão do README será atualizada **após** o bump, no Passo 3.

### Passo 3 — Fazer o version bump

- Se nível foi fornecido explicitamente no Passo 1, usar diretamente.
- Caso contrário, auto-detectar com base na descrição e/ou `git diff`:
  - `feat` → `minor`
  - `fix` → `patch`
  - `refactor` / `chore` / `docs` → `build`

Executar o script:
```powershell
.\scripts\bump-version.ps1 -Type <nível>
```

O script atualiza `Craftimizer/Craftimizer.csproj` e exibe `Version bumped: X.Y.Z.W → X.Y.Z.W`.
Ler a nova versão como `$VERSION`.

Após o bump, atualizar também a linha de versão no README:
```
**Versão atual:** $VERSION · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0
```

### Passo 3.5 — Atualizar backlog/PROGRESS.md

Verificar se a mudança implementa um item rastreado em `backlog/PROGRESS.md`:

1. Inspecionar a descrição do commit, os arquivos do diff e os nomes de arquivos `backlog/*.md` que sejam relevantes para identificar o item correspondente na tabela **Pendente**
2. Se um item correspondente existir em **Pendente**:
   - Adicionar uma linha na tabela **Histórico Completo** com o título curto do item e a versão `$VERSION`
   - Remover a linha correspondente da tabela **Pendente**
   - Atualizar a data "Última revisão" para hoje
   - Incluir `backlog/PROGRESS.md` no staging do Passo 6
3. Se nenhum item de backlog for identificado (ex: hotfix, chore, polish), pular este passo

---

### Passo 4 — Determinar tipo do commit e escopo

Com base em `$ARGUMENTS` e/ou `git diff`:

| Mudança | Tipo | Escopo sugerido |
|---|---|---|
| Correção de bug | `fix` | `ui`, `simulator`, `solver`, `hooks`, `config` |
| Nova funcionalidade | `feat` | idem |
| Refactor / limpeza | `refactor` | idem |
| Manutenção / scripts | `chore` | `build`, `config`, `scripts` |
| Documentação | `docs` | — |
| Breaking change | `feat!` ou `fix!` | idem |

### Passo 5 — Montar mensagem de commit

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

### Passo 6 — Staging e commit

```powershell
# Staged o que foi modificado (código + README + .csproj + PROGRESS.md se atualizado)
git add <arquivos alterados>
git add README.md
git add Craftimizer/Craftimizer.csproj
# Se o Passo 3.5 atualizou o PROGRESS.md:
git add backlog/PROGRESS.md

# Commit
git commit -m "<mensagem montada no Passo 4>"
```

### Passo 7 — Push do commit

```powershell
git push origin main
```

Se o push falhar por o remote estar à frente (ex: `[rejected] … fetch first`):

```powershell
git pull --rebase origin main
git push origin main
```

Verificar que o push foi aceito antes de continuar. **Não criar a tag até o push ter sucesso.**

### Passo 8 — Criar e subir a tag (somente após push bem-sucedido)

Só depois de confirmar que `git push origin main` foi aceito:

```powershell
git tag -a "v$VERSION" -m "Release $VERSION - <título curto>"
git push origin "v$VERSION"
```

O título curto da tag deve ser o mesmo do commit, sem o prefixo convencional.

**Exemplo:** `git tag -a v2.10.3.0 -m "Release 2.10.3.0 - Cosmic Tool progress em tempo real"`

> Nunca usar `git push origin main --tags` — isso sobe todas as tags locais de uma vez e pode publicar tags de trabalho em progresso acidentalmente.

### Passo 9 — Confirmar ao usuário

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
