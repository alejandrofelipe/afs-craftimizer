# Craftimizer Skills — Dalamud Plugin Development

Este diretório contém **skills customizadas** específicas para desenvolvimento e manutenção do plugin Craftimizer (FFXIV Dalamud).

## Estrutura

```
skills/
├─ ffxiv-patch-compatibility-check/
│  └─ SKILL.md
├─ dalamud-plugin-deploy/
│  └─ SKILL.md
├─ ffxiv-memory-offset-debug/
│  └─ SKILL.md
├─ craftimizer-version-bump/
│  └─ SKILL.md
└─ README.md (este arquivo)
```

## Skills Disponíveis

### 1. ffxiv-patch-compatibility-check

**Objetivo:** Analisar compatibilidade do plugin com novas versões do FFXIV  
**Quando usar:** Antes/após release de patch FFXIV, quando Dalamud SDK é atualizado  
**Output:** 3 documentos markdown (viabilidade, análise detalhada, guia rápido), matriz de risco

**Triggers:**
- "verificar compatibilidade patch 7.55"
- "analisar breaking changes patch"
- "preparar para manutenção do jogo"
- "compatibility check FFXIV"

**Processo:**
1. Verifica Dalamud SDK version e compatibilidade semântica
2. Mapeia componentes de risco (CSRecipeNote, SynthHelper, EventFramework)
3. Cria matriz 3D de risco (componente × funcionalidade × cenário)
4. Define procedimentos de verificação in-game
5. Documenta plano de rollback

**Critérios de sucesso:**
- SDK version verificado
- Componentes de alto risco documentados
- Matriz de risco com probabilidade × impacto
- Procedimentos de teste definidos
- Conclusão acionável (compatível/aguardar/refatorar)

---

### 2. dalamud-plugin-deploy

**Objetivo:** Build e deploy do plugin para XIVLauncher  
**Quando usar:** Após implementar feature/fix, para testar in-game, antes de criar release  
**Output:** Plugin compilado em %APPDATA%\XIVLauncher\installedPlugins\Craftimizer\{version}\

**Triggers:**
- "fazer deploy do plugin"
- "build e instalar"
- "compilar Craftimizer"
- "deploy para XIVLauncher"

**Processo:**
1. Executa build Release configuration
2. Lê versão de Craftimizer.csproj
3. Cria diretório de destino
4. Copia todos arquivos (DLL + dependencies)
5. Verifica sucesso do deploy

**Métodos disponíveis:**
- **Script automatizado:** `.\scripts\build.ps1 -Deploy` (recomendado)
- **Manual:** Step-by-step PowerShell
- **Package:** Cria ZIP para distribuição

**Verificação pós-deploy:**
- Arquivo principal existe
- Manifesto JSON válido
- Dependências presentes (8-10 DLLs)
- Versão correta

---

### 3. ffxiv-memory-offset-debug

**Objetivo:** Diagnosticar e corrigir memory offsets quebrados após patch  
**Quando usar:** Plugin não detecta receita, overlay não aparece, dados incorretos  
**Output:** Offset corrigido, código atualizado, documentação de mudança

**Triggers:**
- "offset quebrado"
- "CSRecipeNote não funciona"
- "struct offset inválido"
- "debug memory layout"
- "RecipeNote struct changed"

**Processo:**
1. Identifica offset quebrado (logs, sintomas)
2. Encontra novo offset (FFXIVClientStructs, CheatEngine, ReClass.NET, comunidade)
3. Valida novo offset in-game
4. Documenta mudança (commit + histórico)

**Componentes de alto risco:**
- `CSRecipeNote.cs` → `ActiveCraftRecipeId` @ 0x118
- `Gearsets.cs` → `RaptureGearsetModule.Entries`
- `Hooks.cs` → `Character.StatusList`

**Ferramentas:**
- CheatEngine (memory scanning)
- ReClass.NET (struct visualization)
- x64dbg (assembly debugging)
- FFXIVClientStructs repo (community updates)

---

### 4. craftimizer-version-bump

**Objetivo:** Incrementar versão do plugin seguindo convenções semânticas  
**Quando usar:** Antes de commit de feature/fix, antes de criar release tag  
**Output:** Craftimizer.csproj atualizado com nova versão, staged para commit

**Triggers:**
- "bumpar versão"
- "atualizar versão do plugin"
- "incrementar versão"
- "preparar release"

**Formato de versão:** `MAJOR.MINOR.PATCH.BUILD`

**Regras de incremento:**

| Tipo de Mudança | Componente | Exemplo |
|---|---|---|
| Breaking change | MAJOR | 2.10.0.2 → 3.0.0.0 |
| Update patch FFXIV / Nova feature grande | MINOR | 2.10.0.2 → 2.11.0.0 |
| Bug fix / Nova feature pequena | PATCH | 2.10.0.2 → 2.10.1.0 |
| Refactor / Fix trivial | BUILD | 2.10.0.2 → 2.10.0.3 |

**Processo:**
1. Executa `.\scripts\bump-version.ps1 -Type {major|minor|patch|build}`
2. Lê versão atual de .csproj
3. Incrementa componente especificado
4. Zera componentes inferiores (se aplicável)
5. Atualiza XML
6. Deixa staged para commit

**Mapeamento com Conventional Commits:**
- `feat!:`, `BREAKING CHANGE:` → MAJOR
- `feat(scope):` (major feature) → MINOR
- `feat(scope):`, `fix(scope):` → PATCH
- `refactor:`, `chore:`, `perf:` → BUILD

---

## Como Usar Skills

### Invocação Direta

No chat com GitHub Copilot:

```
"Use a skill ffxiv-patch-compatibility-check para analisar patch 7.55"
"Execute a skill dalamud-plugin-deploy"
"Ajude-me com a skill ffxiv-memory-offset-debug, CSRecipeNote não funciona"
```

### Invocação Automática

Skills são automaticamente acionadas por palavras-chave (triggers). O agente identifica a intenção e executa a skill apropriada.

### Encadeamento de Skills

Workflow típico de atualização de patch:

```
1. ffxiv-patch-compatibility-check (análise)
2. ffxiv-memory-offset-debug (se necessário)
3. craftimizer-version-bump (incrementar MINOR)
4. dalamud-plugin-deploy (testar)
```

## Convenções de Documentação

Cada skill SKILL.md contém:

1. **Frontmatter YAML**
   - `name:` identificador único
   - `description:` resumo de 2-3 linhas
   - Triggers e NOT for (escopo)

2. **Seções principais**
   - Objetivo
   - Quando Usar
   - Pré-requisitos
   - Procedimento (step-by-step)
   - Output Esperado
   - Critérios de Sucesso
   - Troubleshooting
   - Referências

3. **Exemplos práticos**
   - Comandos PowerShell
   - Casos de uso reais
   - Outputs esperados

## Manutenção

### Quando atualizar uma skill:

- Novo padrão de problema descoberto
- Ferramenta ou script adicionado/modificado
- Processo de desenvolvimento mudou
- Feedback de uso revelou gaps

### Como atualizar:

1. Editar SKILL.md correspondente
2. Adicionar nota "Atualizado em: YYYY-MM-DD"
3. Documentar mudança em commit message
4. Notificar em CHANGELOG (se houver)

## Hierarquia de Documentação

```
.github/
├─ agents/
│  └─ craftimizer-dalamud.agent.md     ← Agent principal (modo de operação)
├─ instructions/
│  └─ craftimizer-conventions.instructions.md  ← Convenções de código (apply-to rules)
└─ skills/
   ├─ ffxiv-patch-compatibility-check/  ← Procedimentos especializados
   ├─ dalamud-plugin-deploy/
   ├─ ffxiv-memory-offset-debug/
   └─ craftimizer-version-bump/
```

**Agent** → Define comportamento e contexto geral  
**Instructions** → Regras aplicadas automaticamente a arquivos  
**Skills** → Procedimentos especializados invocáveis

## Referências Externas

- **Dalamud Docs:** https://dalamud.dev/
- **FFXIVClientStructs:** https://github.com/aers/FFXIVClientStructs
- **Dalamud Discord:** https://discord.gg/3NMcUV5 (#plugin-dev)
- **Conventional Commits:** https://www.conventionalcommits.org/
- **Semantic Versioning:** https://semver.org/

## Histórico de Versões

| Data | Versão | Mudanças |
|---|---|---|
| 2026-06-02 | 1.0.0 | Criação inicial com 4 skills base |

---

**Autor:** alejandrofelipe  
**Projeto:** Craftimizer (fork de Asriel Camora)  
**Repositório:** https://github.com/alejandrofelipe/afs-craftimizer
