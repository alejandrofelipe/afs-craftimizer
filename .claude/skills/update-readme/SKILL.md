---
name: update-readme
description: Atualiza a versão no README.md e adiciona entrada em "Diferenças deste Fork" se a mudança for visível ao usuário. Usar após bump de versão ou ao documentar uma mudança no README. Também chamado pelo fluxo de commit.
---

# /update-readme

Atualiza o README.md com a versão atual do plugin e, se a mudança for relevante para usuários, adiciona uma entrada em "Diferenças deste Fork".

Chamado automaticamente pelo `/commit`. Pode ser chamado isolado quando necessário.

---

## Instruções

### 1. Ler versão atual

```powershell
$xml = [xml](Get-Content Artificer/Artificer.csproj)
$version = $xml.Project.PropertyGroup[0].Version
# ex: "2.10.2.0"
```

### 2. Atualizar a linha de versão no README.md

Localizar a linha:
```
**Versão atual:** X.Y.Z.W · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0
```

Substituir apenas o número `X.Y.Z.W` pela versão lida do `.csproj`. Manter `FFXIV 7.51+` e `Dalamud.NET.Sdk 15.0.0` inalterados **a não ser** que a mudança atual envolva atualização de SDK ou compatibilidade com novo patch do FFXIV — nesse caso atualizar também.

### 3. Avaliar se "Diferenças deste Fork" precisa de nova entrada

A seção fica em `## Diferenças deste Fork`. Adicionar um novo bullet **somente se** a mudança for:

| Adicionar | Não adicionar |
|---|---|
| Nova funcionalidade visível ao usuário | Bug fix interno sem impacto visual |
| Correção de comportamento visível (ex: crash corrigido) | Refactor / reorganização de código |
| Melhoria de performance perceptível | Bump de versão de dependência sem impacto |
| Nova integração ou sistema | Ajuste de CI/CD, scripts, backlog |

**Formato do bullet:**
```markdown
- [Descrição da mudança em uma linha, sem jargão técnico, focada no benefício ao usuário]
```

**Exemplos:**
- ✅ `- Correção de crash ao reabrir o Crafting Log após longa inatividade`
- ✅ `- Suporte a Cosmic Exploration: exibe progresso de research data durante Stellar Missions`
- ❌ `- Refactor de RecipeNote.cs para remover campos cached` (interno)
- ❌ `- Bump Dalamud.NET.Sdk 15.0.0 → 15.1.0` (só adicionar se houve mudança funcional junto)

### 4. Resultado esperado

- `README.md` com versão atualizada na linha de header
- Nova entrada em "Diferenças deste Fork" **se e somente se** a mudança for visível ao usuário
- Informar ao usuário o que foi alterado no README (ou que nenhuma alteração em "Diferenças" foi necessária)
