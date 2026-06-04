# /commit

Prepara e executa o commit completo de uma mudança no Craftimizer:
atualiza README, cria commit com mensagem estruturada, cria tag de versão e faz push.

**Uso:** `/commit <descrição curta do que foi feito>`

O argumento `$ARGUMENTS` descreve a mudança — se omitido, inspecionar o `git diff --staged` para inferir.

---

## Instruções

### Passo 1 — Verificar se o version bump foi feito

```powershell
$xml = [xml](Get-Content Craftimizer/Craftimizer.csproj)
$version = $xml.Project.PropertyGroup[0].Version
```

Verificar se há mudança pendente (não staged) no `.csproj`:
```powershell
git diff Craftimizer/Craftimizer.csproj
git diff --cached Craftimizer/Craftimizer.csproj
```

- Se o `.csproj` **não foi modificado** desde o último commit → avisar o usuário e sugerir `/version-bump` antes de continuar. **Não prosseguir sem confirmação.**
- Se já foi modificado (staged ou unstaged) → usar a versão lida como `$VERSION`.

### Passo 2 — Atualizar o README

Executar o fluxo do comando `/update-readme`:
- Atualizar a linha de versão com `$VERSION`
- Avaliar se a mudança merece nova entrada em "Diferenças deste Fork"
  - Sim: adicionar o bullet e incluir `README.md` no commit
  - Não: README só com a linha de versão atualizada; incluir `README.md` no commit mesmo assim

### Passo 3 — Determinar tipo do commit e escopo

Com base em `$ARGUMENTS` e/ou `git diff --staged`:

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
fix(ui): corrigir crash ao reabrir Crafting Log após inatividade

Badges armazenados como campos fixos na janela ficavam com referências
inválidas após o IconManager expirar as texturas do cache. DrawRecipeStats()
tentava acessar .Handle de objeto já disposed.

- Removidos campos CosmicExplorationBadge, SplendorousBadge, SpecialistBadge,
  NoManipulationBadge de RecipeNote.cs
- Texturas agora buscadas via GetIconCached/GetAssemblyTextureCached no draw

Version: 2.10.2.0

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

**Exemplo:** `git tag -a v2.10.2.0 -m "Release 2.10.2.0 - Fix crash ao reabrir Crafting Log"`

### Passo 7 — Push do commit e das tags

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

### Sem mudanças staged
Se `git status` não mostrar nada staged:
1. Verificar se há mudanças unstaged relevantes
2. Sugerir ao usuário quais arquivos adicionar
3. Não criar commit vazio

### Version bump não feito
Se `.csproj` não foi alterado, perguntar:
> "O version bump não foi feito ainda. Que tipo de mudança é esta? (feat/fix/refactor/chore) para eu rodar `/version-bump` com o tipo correto."
Após resposta, executar `/version-bump` e continuar.

### Múltiplas mudanças não relacionadas
Se o diff cobrir mudanças de escopos distintos sem relação, alertar o usuário e sugerir commits separados em vez de um commit grande.
