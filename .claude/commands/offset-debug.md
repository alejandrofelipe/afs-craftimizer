# /offset-debug

Diagnostica e corrige memory offsets quebrados em structs FFXIVClientStructs após atualização do FFXIV.

## Quando Usar

- Plugin não detecta receita ativa (`CSRecipeNote` quebrado)
- Overlay não aparece após abrir crafting log
- Dados de personagem incorretos após patch
- Mensagem de erro relacionada a leitura de offset nos logs

## Componentes de Alto Risco

| Arquivo | Struct | Campo Crítico | Offset Atual | Uso |
|---|---|---|---|---|
| `CSRecipeNote.cs` | `RecipeNote` | `ActiveCraftRecipeId` | 0x118 | Detecta receita aberta |
| `Gearsets.cs` | `RaptureGearsetModule` | `Entries` | (array) | Lê gear sets |
| `Hooks.cs` | `Character` | `StatusList` | (pointer) | Detecta buffs |

## Fase 1: Identificar Offset Quebrado

### Verificar Logs Dalamud

```powershell
Get-Content "$env:APPDATA\XIVLauncher\dalamud.log" | Select-String "Artificer|Exception|offset"
```

Procurar: `NullReferenceException`, `AccessViolationException`, valor `0x00000000`

### Teste de Hipótese

Adicionar log temporário em `CSRecipeNote.cs`:

```csharp
var recipeId = Instance->ActiveCraftRecipeId;
Service.Log.Debug($"RecipeNote offset 0x118 = {recipeId}");
// Esperado: 0 quando fechado, 1-9999 quando receita selecionada
// Se sempre 0 ou valor absurdo → offset está errado
```

**Antes de assumir offset quebrado: tentar `dotnet clean` + rebuild (pode ser cache corrompido)**

## Fase 2: Encontrar Novo Offset

### Método 1: Consultar FFXIVClientStructs (Recomendado)

Ver commits recentes em: https://github.com/aers/FFXIVClientStructs/commits/main

Se encontrado, atualizar em `CSRecipeNote.cs`:

```csharp
[FieldOffset(0x120)]  // ← novo offset
public ushort ActiveCraftRecipeId;
```

### Método 2: CheatEngine (Análise Manual)

1. Attach no `ffxiv_dx11.exe`
2. Abrir crafting log, selecionar receita com ID conhecido
3. Scan: "Exact Value" = recipe ID
4. Trocar receita → "Next Scan" com novo ID
5. Repetir até endereço estável
6. Calcular: `offset = endereço_campo - endereço_base_RecipeNote`

### Método 3: Comunidade

- Discord Dalamud, canal `#plugin-dev`
- Issues em: https://github.com/aers/FFXIVClientStructs/issues

## Fase 3: Validar

Após aplicar novo offset:
1. Compilar e deployar (`/deploy`)
2. Iniciar FFXIV com Dalamud
3. Abrir crafting log → selecionar receita → overlay deve aparecer
4. Fechar crafting log → overlay deve desaparecer

## Fase 4: Documentar

```powershell
git add Artificer/Utils/Infrastructure/CSRecipeNote.cs
git commit -m "fix(memory): corrigir offset ActiveCraftRecipeId após patch X.Y

- Offset antigo: 0x118
- Offset novo: 0x120
- Causa: campo adicionado antes no struct
- Ref: https://github.com/aers/FFXIVClientStructs/commit/abc123

Fixes #XXX"
```

## Padrões Comuns de Mudança

**Campo adicionado antes:**
```
// Patch 7.5:  ushort ActiveCraftRecipeId  @ 0x118
// Patch 7.51: uint NewField @ 0x118, ushort ActiveCraftRecipeId @ 0x11C
```

**Mudança de tipo:** `byte` → `ushort` desloca todos campos seguintes em +1 byte

**Padding automático:** Compilador C++ alinha campos; `int` após `byte` gera 3 bytes de padding

## Ferramentas

- **CheatEngine**: https://www.cheatengine.org/ — memory scanning
- **ReClass.NET**: https://github.com/ReClassNET/ReClass.NET — struct visualization
- **x64dbg**: https://x64dbg.com/ — debugging em nível de assembly

## Prevenção

Usar SigScanner em vez de offsets hardcoded quando possível:

```csharp
var signature = "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 ??";
var address = Service.SigScanner.ScanText(signature);
```
