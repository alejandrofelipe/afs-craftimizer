---
name: backlog
description: Cria um novo item de backlog do Artificer (bug, rascunho ou feature completa) seguindo os templates e registra em backlog/PROGRESS.md. Usar quando o usuário pedir para registrar/anotar um item de backlog, abrir um bug, anotar uma ideia ou detalhar uma feature.
---

# /backlog

Cria um novo item de backlog para o Artificer, seguindo os templates estabelecidos, e registra em `backlog/PROGRESS.md`.

**Uso:** `/backlog <título ou descrição do item>`

---

## Instruções

Receba o argumento `$ARGUMENTS` e siga os passos abaixo:

### 1. Determinar tipo e profundidade

Classifique o item com base na descrição fornecida:

| Tipo | Quando usar | Template |
|---|---|---|
| **bug** | Crash, exceção, comportamento incorreto, regressão | Bug |
| **feature-rascunho** | Ideia sem detalhes claros, questões em aberto | Rascunho |
| **feature** | Feature com requisitos definidos e contexto técnico | Feature completa |

Se não for possível determinar com certeza, perguntar ao usuário: "É um bug, uma ideia rápida ou uma feature para detalhar?"

### 2. Gerar nome do arquivo

- Formato: `backlog/<slug-kebab-case>.md`
- Para bugs: prefixar com `bug-` → `backlog/bug-<slug>.md`
- Slug: lowercase, sem acentos, palavras separadas por `-`, máximo 6 palavras
- Exemplos:
  - "Bug crash ao abrir macro editor" → `bug-crash-macro-editor.md`
  - "Mostrar tempo estimado de craft" → `mostrar-tempo-estimado-craft.md`
  - "Calcular custo de materiais" → `calcular-custo-materiais.md`

> **Mockups HTML e arquivos de design** (`.html`, `.fig`, imagens de referência) devem ser criados em `mockup/`, nunca em `backlog/`. O arquivo de backlog pode referenciar o mockup com um link relativo, ex: `[Mockup](../mockup/nome-da-feature-mockup.html)`.

### 3. Pesquisar na internet (quando relevante)

Antes de criar o arquivo, use `WebSearch` e/ou `WebFetch` se a feature ou bug envolver:

- **APIs ou SDKs externos** — buscar documentação atual (ex: Dalamud API, Lumina, FFXIVClientStructs)
- **Mecânicas do jogo** — consultar wikis (Consolegameswiki, FFXIV Wiki) para confirmar nomes, IDs ou comportamentos
- **Bibliotecas/pacotes NuGet** — verificar versão mais recente e compatibilidade
- **Features do plugin original** — checar se o upstream (github.com/WorkingRobot/Craftimizer) já implementou ou discutiu algo parecido

**Queries úteis de exemplo:**
- `"Dalamud <recurso> API site:goatcorp.github.io"`
- `"FFXIVClientStructs <struct> site:github.com"`
- `"<nome da receita ou mechânica> FFXIV wiki"`

Incluir os links encontrados na seção **Referências** do template. Se não houver nada relevante para buscar, pular esta etapa.

### 4. Criar o arquivo com o template adequado

---

#### Template: Bug

```markdown
# Bug — [Título descritivo do bug]

**Criado:** YYYY-MM-DD
**Status:** 🔴 Bug confirmado
**Tipo:** Bug / Crash

---

## Stack trace

```
[Colar stack trace aqui se disponível, ou "N/A"]
```

## Análise do Problema

[Descrever o que causa o bug. Qual componente está envolvido, qual é o estado que leva ao problema, por que acontece.]

## Solução Proposta

[Passos para corrigir:]
1. ...
2. ...

## Arquivos Afetados

- `Artificer/[Arquivo].cs`

## Status

[Estado atual: pronto para corrigir / aguarda investigação / aguarda info]
```

---

#### Template: Feature — Rascunho

```markdown
# Backlog — [Título da feature]

**Criado:** YYYY-MM-DD
**Status:** 📝 Rascunho — aguarda detalhamento
**Tipo:** Feature

---

## Resumo

[1-3 frases descrevendo a ideia.]

## Questões em aberto

- [O que ainda não está claro sobre como implementar?]
- [Há dependências externas?]
- [Qual é o escopo: nova janela, widget inline, hook?]

## Status

⏳ Aguarda detalhamento.
```

---

#### Template: Feature — Completa

```markdown
# Backlog — [Título da feature]

**Criado:** YYYY-MM-DD
**Status:** 📝 Refinado
**Tipo:** Nova feature
**Estimativa total:** X–Yh

---

## Resumo Executivo

[2-4 frases. O que é a feature, qual problema resolve, o que entrega ao usuário.]

---

## Problema

[Por que essa feature é necessária? O que o usuário não consegue fazer hoje?]

---

## Objetivo

[O que a feature entrega. Incluir formato alvo / mockup de texto se aplicável.]

```
[mockup ou exemplo de output]
```

---

## Plugins Externos

> [Se não depende de nenhum plugin externo, escrever: "Esta feature não depende de nenhum plugin externo."]
>
> Se depender, listar na tabela:

| Plugin | Sub-feature | Comportamento sem o plugin |
|---|---|---|
| ... | ... | ... |

---

## Escopo da Feature

### O que inclui
- ...

### O que não inclui (fora do escopo inicial)
- ...

---

## Arquitetura

### Novos Arquivos

```
Artificer/
  [Listar arquivos novos com breve descrição]
```

### Arquivos Modificados

| Arquivo | Motivo |
|---|---|
| `...` | ... |

---

## Fases de Implementação

### Fase 0 — Investigação (Xh)
- [ ] ...

### Fase 1 — [Nome] (X–Yh)
- [ ] ...

### Fase 2 — [Nome] (X–Yh)
- [ ] ...

---

## Critérios de Aceite

- [ ] ...
- [ ] ...

---

## Riscos

| Risco | Probabilidade | Mitigação |
|---|---|---|
| ... | Baixo/Médio/Alto | ... |

---

## Referências

- [Nome](URL)
```

---

### 5. Atualizar `backlog/PROGRESS.md`

Adicionar uma linha na tabela **Pendente** em `backlog/PROGRESS.md`:

| Tipo | Linha a adicionar |
|---|---|
| Bug | `\| 🔴 Bug: [Título curto] \| 🔴 Bug confirmado \| Ver \`backlog/[arquivo].md\` \|` |
| Feature rascunho | `\| [Título curto] \| 📝 Rascunho \| Ver \`backlog/[arquivo].md\` \|` |
| Feature completa | `\| [Título curto] \| 📝 Refinado \| Ver \`backlog/[arquivo].md\` \|` |

---

### 6. Confirmar ao usuário

Informar:
- Arquivo criado: `backlog/[arquivo].md`
- Entrada adicionada em `backlog/PROGRESS.md`
- Se for rascunho: oferecer detalhar a feature agora ("Quer que eu detalhe mais a feature?")
- Se for feature completa: mencionar que as fases de implementação foram deixadas em aberto para ajuste
