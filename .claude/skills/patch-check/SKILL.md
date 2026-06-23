---
name: patch-check
description: Analisa a compatibilidade do plugin Artificer com uma nova versão do FFXIV, produzindo documentação técnica e matriz de risco. Usar antes ou logo após um patch do FFXIV ou update do Dalamud SDK.
---

# Patch Check

Analisa a compatibilidade do plugin Artificer com uma nova versão do FFXIV. Produz documentação técnica e matriz de risco.

> Para **corrigir** um offset confirmado como quebrado, usar a skill `offset-debug`.

## Quando Usar

- Antes ou logo após release de patch do FFXIV (X.Y, X.YZ)
- Quando Dalamud SDK é atualizado
- Quando há reports de plugin não funcionando após manutenção

## Fase 1: Análise de SDK e Dependências

```powershell
# SDK atual
Get-Content Artificer/Artificer.csproj | Select-String "Dalamud.NET.Sdk"

# Todas as dependências
Get-Content Artificer/Artificer.csproj | Select-String "PackageReference"
```

Verificar compatibilidade do SDK em: https://github.com/goatcorp/Dalamud.NET.Sdk/releases

Dependências críticas:
- `Dalamud.NET.Sdk` — SDK principal (afetado por patches do jogo)
- `Microsoft.Data.Sqlite` — storage de macros
- `Microsoft.Extensions.Caching.Memory` — icon cache
- `Raphael.Net` — não afetado por patches do jogo

## Fase 2: Mapeamento de Componentes de Risco

### Alto Risco (8-10%)
- `Artificer/Utils/Infrastructure/CSRecipeNote.cs`
  - `[FieldOffset(0x118)] public ushort ActiveCraftRecipeId`
  - Teste: Abrir crafting log, selecionar receita

### Médio Risco (5-7%)
- `Artificer/Windows/SynthHelper.cs`
  - Lê 26 AtkValue indices do `AddonSynthesis`
  - Teste: Iniciar craft, verificar suggestions

### Baixo Risco (1-3%)
- `Artificer/Utils/SimulatorUtils.cs`
  - Status IDs: 48 (Well Fed), 49 (Medicated), 356, 357 (FC buffs)
  - Teste: Comer comida, verificar detection
- `Artificer/Utils/RecipeData.cs`
  - Lumina sheets auto-updated pelo Dalamud

## Fase 3: Documentação de Análise

Criar em `backlog/`:

1. **VIABILIDADE-UPDATE-X.Y.md** — análise inicial
   - Resumo executivo
   - Status do SDK e dependências
   - Conclusão de compatibilidade (%, confiança)

2. **UPDATE-X.Y-DETAILED-ANALYSIS.md** — análise técnica
   - 27 FFXIVClientStructs mapeados
   - 26 AtkValue indices documentados
   - Matriz de risco 3D

3. **GUIA-RAPIDO-UPDATE-X.Y.md** — guia de execução
   - Procedimento passo a passo
   - 5 testes manuais in-game
   - Troubleshooting (6 cenários comuns)

## Fase 4: Matriz de Risco

| Componente | Funcionalidade | Probabilidade | Impacto | Prioridade |
|---|---|---|---|---|
| CSRecipeNote.cs | Recipe detection | 8% | CRÍTICO | P0 |
| SynthHelper.cs | Mid-craft suggestions | 6% | ALTO | P1 |
| EventFramework | Cosmic Exploration | 10% | MÉDIO | P2 |

## Fase 5: Testes In-Game

1. **Smoke test**: `/xlplugins` → Artificer está loaded
2. **RecipeNote test**: `/craftlog` → selecionar receita → overlay aparece
3. **SynthHelper test**: Iniciar craft → suggestions aparecem
4. **New content test**: Testar receitas/NPCs novos do patch
5. **Cosmic Exploration test** (se aplicável): Hooks de EventFramework funcionam

## Fase 6: Plano de Rollback

```powershell
$oldVersion = "2.X.X.X"
$pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Artificer"
Remove-Item "$pluginDir\*" -Recurse -Force
Copy-Item "backup\$oldVersion\*" $pluginDir -Recurse -Force
```

## Output Esperado

- 3 documentos markdown criados em `backlog/`
- Matriz de risco classificando 10-15 componentes
- 5 procedimentos de teste manuais definidos
- Plano de rollback documentado
- Conclusão acionável: "compatível", "requer mudanças", ou "aguardar SDK update"

## Histórico de Breaking Changes

| Patch | Componente Afetado | Solução |
|---|---|---|
| 7.0 (Dawntrail) | Major SDK bump (14 → 15) | Atualizar SDK, recompilar |
| 7.1 | Nenhum | Zero changes |
| 7.5 | Adição Cosmic Exploration | Adicionar EventFramework support |
| 7.51 | Nenhum | Zero changes (confirmado) |

## Referências Técnicas

- 27 structs FFXIVClientStructs cobertas: `RecipeNote`, `UIState`, `PlayerState`, `AddonSynthesis`, `AddonRecipeNote`, `EventFramework`, `Character`, `InventoryContainer`, etc.
- 26 índices AtkValue críticos para `SynthHelper.cs`:
  - Index 0-7: Materiais e qualidade
  - Index 8-15: Progresso, durabilidade, CP
  - Index 16-21: Condition, buffs, step count
  - Index 22-25: Collectability ranges
