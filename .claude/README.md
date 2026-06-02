# 🤖 Claude Desktop Configuration — Craftimizer

Este diretório contém toda a configuração para uso do projeto **Craftimizer** com o **Claude Desktop** (aplicação standalone).

## 📁 Estrutura

```
.claude/
├─ project.json          ← Configuração principal do projeto Claude
├─ agents/               ← Agentes especializados (modos personalizados)
│  └─ craftimizer-dalamud.agent.md
├─ instructions/         ← Regras aplicadas automaticamente
│  └─ craftimizer-conventions.instructions.md
├─ skills/               ← Procedimentos especializados invocáveis
│  ├─ README.md
│  ├─ craftimizer-version-bump/
│  ├─ dalamud-plugin-deploy/
│  ├─ ffxiv-memory-offset-debug/
│  └─ ffxiv-patch-compatibility-check/
├─ prompts/              ← Prompts reutilizáveis
│  └─ atualizar-craftimizer.prompt.md
└─ workflows/            ← Automações
   └─ build.yml
```

---

## 🎯 Configuração Principal

### project.json

Define as preferências do projeto para Claude Desktop:

```json
{
  "preferences": {
    "model": "claude-sonnet-4.5",        ← Modelo prioritário
    "defaultAgent": "craftimizer-dalamud", ← Agent padrão
    "temperature": 0.7,
    "maxTokens": 8192
  }
}
```

**Efeito:** Quando você abrir este projeto no Claude Desktop, ele automaticamente:
- ✅ Usará **Claude Sonnet 4.5**
- ✅ Carregará o agent **craftimizer-dalamud**
- ✅ Aplicará contexto completo do projeto

---

## 🤖 Agent: craftimizer-dalamud

**Arquivo:** `agents/craftimizer-dalamud.agent.md`

**Modelo configurado:** `claude-sonnet-4.5`

**Propósito:** Especialista em desenvolvimento do plugin Craftimizer para FFXIV via Dalamud.

**Capacidades:**
- 🔧 Atualizar plugin para novas versões do jogo
- 🐛 Corrigir breaking changes do Dalamud SDK
- ⚙️ Modificar lógica do simulador de crafting
- 📊 Atualizar Lumina sheets
- 🔗 Trabalhar com FFXIVClientStructs
- 🧮 Ajustar solver MCTS/Raphael
- 🪝 Debug de hooks e interop com o jogo
- 🏗️ Builds e testes do plugin

---

## 📚 Skills Especializadas (4 disponíveis)

### 1. ffxiv-patch-compatibility-check
Análise de compatibilidade com patches FFXIV  
**Triggers:** "verificar compatibilidade patch", "analisar breaking changes"

### 2. dalamud-plugin-deploy
Build e deploy automatizado para XIVLauncher  
**Triggers:** "fazer deploy", "build e instalar", "compilar"

### 3. ffxiv-memory-offset-debug
Debug de memory offsets quebrados  
**Triggers:** "offset quebrado", "CSRecipeNote não funciona"

### 4. craftimizer-version-bump
Gerenciamento semântico de versão  
**Triggers:** "bumpar versão", "incrementar versão"

**📖 Documentação completa:** `skills/README.md`

---

## 📋 Instructions (Convenções de Código)

**Arquivo:** `instructions/craftimizer-conventions.instructions.md`

**Apply-to:** `**/*.cs`, `**/*.csproj`, `**/*.json`

**Regras aplicadas automaticamente:**
- ✅ Lumina access via `LuminaSheets.GetSheet<T>()` apenas
- ✅ Service DI pattern com `[PluginService]`
- ✅ Unsafe blocks apenas em Craftimizer/ project
- ✅ Versionamento: MAJOR.MINOR.PATCH.BUILD
- ✅ Commit style: Conventional Commits

---

## 🚀 Como Usar no Claude Desktop

### 1. Abrir Projeto

No Claude Desktop:
1. **File** → **Open Project Folder**
2. Selecionar: `C:\Users\aleja\DEV\Craftimizer`
3. Claude detectará automaticamente `.claude/project.json`
4. Modelo **Claude Sonnet 4.5** será carregado
5. Agent **craftimizer-dalamud** estará disponível

### 2. Invocar Agent

```
@craftimizer-dalamud verificar compatibilidade com patch 7.55
```

Ou deixar o Claude detectar automaticamente:

```
Preciso atualizar o plugin para a versão 7.55 do FFXIV
→ Claude detecta contexto e invoca craftimizer-dalamud automaticamente
```

### 3. Usar Skills

```
Use a skill ffxiv-patch-compatibility-check para analisar patch 7.55
```

Ou via triggers automáticos:

```
Fazer deploy do plugin
→ Claude invoca automaticamente dalamud-plugin-deploy
```

### 4. Workflows Comuns

#### Update de Patch FFXIV
```
1. "Analisar compatibilidade patch 7.55"
   → skill: ffxiv-patch-compatibility-check
2. (Se necessário) "Corrigir offset CSRecipeNote"
   → skill: ffxiv-memory-offset-debug
3. "Bumpar versão para MINOR"
   → skill: craftimizer-version-bump
4. "Fazer deploy"
   → skill: dalamud-plugin-deploy
```

#### Nova Feature
```
1. Descrever feature desejada
2. Claude implementa usando agent craftimizer-dalamud
3. "Bumpar versão para PATCH"
4. "Fazer deploy"
```

#### Bug Fix
```
1. Descrever bug
2. Claude diagnostica e corrige
3. "Bumpar versão para PATCH"
4. "Fazer deploy"
```

---

## 🔧 Configurações Avançadas

### Trocar Modelo Temporariamente

```
@craftimizer-dalamud (usando claude-opus) implementar nova feature X
```

### Ajustar Temperature

Editar `.claude/project.json`:
```json
"preferences": {
  "temperature": 0.5  // Mais determinístico (0.0-1.0)
}
```

### Adicionar Novo Agent

1. Criar `.claude/agents/novo-agent.agent.md`
2. Adicionar frontmatter YAML:
   ```yaml
   name: novo-agent
   model: claude-sonnet-4.5
   description: >
     Descrição do agente
   ```

### Criar Nova Skill

1. Criar `.claude/skills/nova-skill/SKILL.md`
2. Adicionar frontmatter YAML:
   ```yaml
   name: nova-skill
   description: >
     Descrição da skill
   ```

---

## 📊 Hierarquia de Configuração

```
.clauderc                        ← Config root (formato TOML-like)
.claude/
├─ project.json                  ← Config principal (JSON)
├─ agents/                       ← Modos especializados
├─ skills/                       ← Procedimentos invocáveis
└─ instructions/                 ← Regras automáticas
```

**Precedência:**
1. `.claude/project.json` (mais específico)
2. `.clauderc` (fallback)
3. Defaults do Claude Desktop

---

## 🎓 Diferença: Claude Desktop vs VS Code

| Aspecto | Claude Desktop | VS Code (GitHub Copilot) |
|---------|----------------|--------------------------|
| **Config file** | `.claude/project.json` | `.vscode/settings.json` |
| **Model field** | `"model": "claude-sonnet-4.5"` | `"model": "claude-sonnet-4.5"` |
| **Agent format** | `.agent.md` (YAML frontmatter) | `.agent.md` (YAML frontmatter) |
| **Skills** | `.claude/skills/` | `.github/skills/` |
| **Instructions** | `.claude/instructions/` | `.github/instructions/` |

**Ambos compartilham:**
- ✅ Mesmo formato de agents (`.agent.md`)
- ✅ Mesmas skills (podem referenciar qualquer path)
- ✅ Mesmas instructions

**Este projeto está configurado para ambos!**

---

## 🔍 Troubleshooting

### Claude não detecta configuração

**Solução:**
1. Verificar que `.claude/project.json` existe
2. Recarregar projeto: **File** → **Reload Project**
3. Verificar sintaxe JSON: `cat .claude/project.json | ConvertFrom-Json`

### Model não muda

**Solução:**
1. Editar `.claude/project.json`
2. Trocar `"model": "claude-sonnet-4.5"` para modelo desejado
3. Recarregar projeto

### Agent não carrega

**Solução:**
1. Verificar que `.claude/agents/craftimizer-dalamud.agent.md` existe
2. Verificar frontmatter YAML válido
3. Invocar explicitamente: `@craftimizer-dalamud`

### Skills não funcionam

**Solução:**
1. Verificar path em `project.json`: `"skillsPath": ".claude/skills/"`
2. Verificar estrutura: cada skill em subdiretório com `SKILL.md`
3. Invocar explicitamente: `Use a skill craftimizer-version-bump`

---

## 📚 Referências

- **Claude Desktop Docs:** https://docs.anthropic.com/claude/docs
- **Agent Format:** Markdown com YAML frontmatter
- **Skills Format:** Markdown com YAML frontmatter + procedimentos
- **Instructions Format:** Markdown com regras apply-to

---

## 📝 Manutenção

### Atualizar versão do projeto

Editar `.claude/project.json`:
```json
"version": "2.10.0.3"
```

### Adicionar nova tecnologia

Editar `.claude/project.json`:
```json
"technologies": [
  ...,
  "NovaLibrary 1.0.0"
]
```

### Sincronizar com .github/

As skills e agents existem em dois lugares para compatibilidade:
- `.claude/` → Claude Desktop
- `.github/` → VS Code + GitHub Actions

**Manter sincronizados:** Ao editar, atualizar ambos.

---

**🎉 Configuração completa para Claude Desktop!**  
O projeto está pronto para uso tanto no Claude Desktop quanto no VS Code com GitHub Copilot.

**Autor:** alejandrofelipe  
**Projeto:** Craftimizer (fork de Asriel Camora)  
**Repositório:** https://github.com/alejandrofelipe/afs-craftimizer
