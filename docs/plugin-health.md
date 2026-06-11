# Artificer Plugin — Diagnóstico de Saúde e Guia de Testes

> Última atualização: 2026-06-11 (v2.20.8.0)  
> Baseado em: Dalamud.NET.Sdk 15.0.0 · Dalamud local em `C:\Users\aleja\DEV\dalamud` · ImGuiNET 1.90.9.1

---

## 1. Incompatibilidades Conhecidas e Status

### 1.1 ImGuiStyleVar — Mismatch entre ImGuiNET e Dalamud (RESOLVIDO)

**Causa raiz confirmada via `C:\Users\aleja\DEV\dalamud\imgui\Dalamud.Bindings.ImGui\Generated\Enums\ImGuiStyleVar.cs`:**

Dalamud moveu `DisabledAlpha` do índice 1 para o índice 24 no seu enum `ImGuiStyleVar`, deslocando todos os outros valores -1:

| Nome              | ImGuiNET 1.90.9.1 | Dalamud.Bindings.ImGui |
|-------------------|:-----------------:|:----------------------:|
| Alpha             | 0                 | 0 ← **mesmo**          |
| DisabledAlpha     | 1                 | **24** ← movido        |
| WindowPadding     | 2                 | 1                      |
| WindowRounding    | 3                 | 2                      |
| ChildRounding     | 7                 | 6                      |
| FramePadding      | 11                | 10                     |
| FrameRounding     | 12                | 11                     |
| ItemSpacing       | 14                | 13                     |
| *(todos outros)*  | N                 | N-1                    |

**Sintoma quando não corrigido:** assertion `"Called PushStyleVar() ImVec2 variant but variable is not a ImVec2!"` no log do Dalamud, seguido de crash C0000005 no próximo frame.

**Fix implementado em `Artificer.UI`:**
- `Theme.cs`: static fields `_windowPadding`, `_frameRounding`, `_childRounding` inicializados com valores ImGuiNET e sobrescritos via `Theme.ConfigureForDalamud()` chamado no construtor do plugin.
- `ImRaiiShim.cs`: método `Remap(ImGuiStyleVar idx)` converte qualquer valor ImGuiNET para o valor Dalamud correspondente. Ativado por `ImRaii.ConfigureForDalamud()`, chamado dentro de `Theme.ConfigureForDalamud()`.

**Por que apenas `Artificer.UI` precisa do remap:**
O projeto `Artificer` (plugin principal) usa `Dalamud.Bindings.ImGui` diretamente via Dalamud.NET.Sdk — os enums já têm os valores corretos. O projeto `Artificer.UI` (biblioteca compartilhada, sem SDK Dalamud) usa `ImGuiNET` (package NuGet padrão) — daí a necessidade do remap.

**Regra para novos desenvolvedores:**
> Em `Artificer.UI`, **nunca** chame `ImGui.PushStyleVar(idx, val)` diretamente. Sempre use `ImRaii.PushStyle(idx, val)` que passa pelo `Remap()`.

---

### 1.2 ImGuiCol — Sem Mismatch (SEGURO)

Confirmado via `C:\Users\aleja\DEV\dalamud\imgui\Dalamud.Bindings.ImGui\Generated\Enums\ImGuiCol.cs`:  
Os valores de `ImGuiCol` são **idênticos** entre ImGuiNET e Dalamud.Bindings.ImGui.  
Chamadas diretas a `ImGui.PushStyleColor(ImGuiCol.X, ...)` em qualquer projeto são seguras.

---

### 1.3 cimgui.dll Duplicada (RESOLVIDO)

O plugin não deve shipar sua própria `cimgui.dll`. O Dalamud fornece a DLL nativa com contexto ImGui inicializado (`GImGui`). Shipar uma segunda cópia causaria `GImGui == NULL` → crash C0000005.

**Fix**: target MSBuild `RemoveCimguiDll` em `Artificer/Artificer.csproj` deleta `$(OutDir)cimgui.dll` pós-build.  
`ImGui.NET.dll` (wrapper gerenciado) continua sendo shipada — necessária desde Dalamud SDK 15.

---

## 2. Ciclo de Vida das Janelas — Interação com WindowHost

**Arquivo de referência:** `C:\Users\aleja\DEV\dalamud\Dalamud\Interface\Windowing\WindowHost.cs`

O `WindowHost` do Dalamud envolve cada janela com o seguinte fluxo por frame:

```
WindowHost.DrawInternal()
  1. [Se internalAlpha] → ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ...)  ← usa Dalamud enum
  2. Window.PreDraw()                                                   ← plugin code
  3. ApplyConditionals()                                               ← Dalamud applies conditions
  4. ImGui.Begin(...)
  5. Window.Draw()                                                      ← plugin code
  6. ImGui.End()
  7. Window.PostDraw()                                                  ← plugin code
  8. [Se internalAlpha] → ImGui.PopStyleVar()
```

**Implicações:**
- `Theme.Push()` em `PreDraw()` empilha **17 cores + 3 vars**. `Theme.Pop()` em `PostDraw()` deve desempilhar exatamente esses 20. Qualquer assimetria → assertions do ImGui.
- O `DrawGuard.Try()` em `Draw()` captura exceções gerenciadas (C# exceptions) mas **não captura C0000005** (access violation nativo). Se o crash ocorre em `Draw()`, o DrawGuard previne crashes repetidos; se ocorre em `PreDraw()`/`PostDraw()`, o crash propaga normalmente.
- `internalAlpha` é para janelas semi-transparentes configuradas pelo Dalamud (ex: clique-através). Alpha = 0 é o mesmo em ambos os enums → safe.

---

## 3. Serviços Dalamud — Notas de Compatibilidade

### 3.1 `ClientState.IsLoggedIn` vs `LocalPlayer != null`

**Implementação Dalamud** (`ClientState.cs`):
```csharp
public unsafe bool IsLoggedIn
{
    get
    {
        var agentLobby = AgentLobby.Instance();
        return agentLobby != null && agentLobby->IsLoggedIn;
    }
}
```

`IsLoggedIn` verifica o agente de lobby — retorna `true` quando o personagem está carregado e ativo no mundo.  
`LocalPlayer != null` pode ser `true` momentaneamente durante transições de zona enquanto o agente de lobby ainda reporta `false`.

**Uso no Artificer:** `FeatureHubWindow.Update()` usa `IsLoggedIn` — correto para garantir que a janela só aparece quando o personagem está plenamente em jogo.

### 3.2 Serviços via `[Service]` Attribute

Todos os serviços Dalamud injetados via `[Service]` attribute no `Service.cs` do projeto são compatíveis com SDK 15. A principal mudança no SDK 15 foi:
- `ImGui.NET.dll` foi removida do runtime do Dalamud → o plugin agora a shipa
- `Dalamud.Bindings.ImGui` passou a ser a referência primária para ImGui no SDK

---

## 4. Riscos Arquiteturais Residuais

### 4.1 ⚠️ Acoplamento Implícito: Artificer.UI + Dalamud cimgui

`Artificer.UI` é uma biblioteca "sem Dalamud" mas é executada contra o cimgui do Dalamud. O contrato atual:

```
Artificer.UI compila contra:  ImGuiNET 1.90.9.1 (NuGet, valores padrão)
Artificer.UI executa contra:  cimgui.dll do Dalamud (valores deslocados -1)
Ponte entre os dois:          ImRaiiShim.Remap() + Theme.ConfigureForDalamud()
```

**Risco:** Se o Dalamud mudar novamente os índices do `ImGuiStyleVar`, a função `Remap()` em `ImRaiiShim.cs` precisará ser atualizada. O sintoma seria o mesmo: assertions no log + crash C0000005.

**Como detectar:** A primeira linha do log do Dalamud após o crash sempre conterá `"Called PushStyleVar() ImVec2 variant but variable is not a ImVec2!"` ou `"Called PushStyleVar() float variant but variable is not a float!"`.

**Como verificar a versão atual do Dalamud:**
```bash
# Comparar com o enum em:
C:\Users\aleja\DEV\dalamud\imgui\Dalamud.Bindings.ImGui\Generated\Enums\ImGuiStyleVar.cs
# vs ImGuiNET:
C:\Users\aleja\.nuget\packages\imgui.net\1.90.9.1\lib\net6.0\ImGuiNET.xml
```

### 4.2 ℹ️ UIStudio usa ImGuiNET sem remap

`Artificer.UIStudio` usa `Artificer.UI` mas NÃO chama `Theme.ConfigureForDalamud()` — correto, pois roda contra o cimgui padrão do Silk.NET/GLFW onde os valores ImGuiNET estão corretos. Se o UIStudio for portado para rodar dentro do Dalamud, essa chamada precisará ser adicionada.

---

## 5. Estratégia de Testes

### 5.1 O que existe (e o que não existe)

**Existe:**
- Dalamud v10+ introduziu **interfaces** para todos os serviços principais (`IDalamudPluginInterface`, `IClientState`, `ICommandManager`, etc.) especificamente para permitir mocking em testes
- Frameworks padrão .NET — **NSubstitute** ou **Moq** — podem mockar essas interfaces

**Não existe:**
- Nenhum pacote NuGet oficial `DalamudMock`
- Nenhuma integração com `imgui_test_engine` (o engine de automação do Dear ImGui)
- Nenhuma forma de "headless rendering" completo de ImGui sem contexto DirectX/OpenGL

### 5.2 Camadas de Teste Recomendadas

#### Camada 1 — Lógica pura (já implementado ✓)
`Test/` cobre Simulator + Solver com NUnit. Nenhuma dependência Dalamud. Continuar expandindo aqui.

#### Camada 2 — Serviços com mocks (a implementar)

```xml
<!-- Adicionar ao Test/Artificer.Test.csproj -->
<PackageReference Include="NSubstitute" Version="5.*" />
```

```csharp
// Exemplo: testar que Plugin inicializa sem exceção
[Test]
public void Plugin_Initializes_Without_Exception()
{
    var mockInterface = Substitute.For<IDalamudPluginInterface>();
    mockInterface.ConfigDirectory.Returns(new DirectoryInfo(Path.GetTempPath()));
    // ... configurar outros serviços necessários
    // Plugin(mockInterface) deve não lançar exceção
}
```

**Limitação real:** O construtor de `Plugin` chama `Service.Initialize(pluginInterface)` que depende de uma cadeia de serviços Dalamud registrados via IoC interno. Mockar isso requer ou (a) refatorar o Plugin para injeção de dependência explícita, ou (b) mockar `IServiceProvider` do Dalamud.

#### Camada 3 — Componentes de UI (nunca testar o ImGui diretamente)

O ImGui não é testável unitariamente (requer contexto de render). A abordagem correta é separar **estado** de **renderização**:

```csharp
// Testável — lógica de estado pura
public class CraftingListState
{
    public bool IsEmpty => Items.Count == 0;
    public void AddItem(CraftingItem item) { ... }
    public void RemoveItem(int id) { ... }
}

// Não testável — renderização pura
public partial class CraftingListWindow
{
    private void DrawContent()
    {
        if (_state.IsEmpty) DrawEmptyState();
        else DrawItemList();
    }
}
```

### 5.3 Verificação de Compatibilidade sem Abrir o Jogo

Para detectar crashes de carregamento **antes** de abrir o FFXIV:

1. **Build Release + verificar warnings:**
   ```powershell
   dotnet build Artificer/Artificer.csproj -c Release 2>&1 | Select-String "warning|error"
   ```
   Zero warnings é obrigatório. Qualquer warning pode indicar problema.

2. **Deploy + checar log do Dalamud na próxima abertura:**
   ```powershell
   .\scripts\build.ps1 -Deploy -NoBuild
   # Depois abrir o jogo e verificar em /xllog ou:
   # %APPDATA%\XIVLauncher\dalamud.log
   ```

3. **Monitorar assertions ImGui no log:**
   Qualquer linha contendo `"PushStyleVar"`, `"PopStyleVar"`, `"PushStyleColor"`, `"assert"` indica problema de estilo imediato.

4. **Testar crash de carregamento de assembly:**
   Verificar que todos os DLLs necessários estão no output:
   ```powershell
   Get-ChildItem "Artificer\bin\Release" -Filter "*.dll" | Select-Object Name
   # Deve conter: Artificer.dll, Artificer.UI.dll, ImGui.NET.dll, etc.
   # NÃO deve conter: cimgui.dll
   ```

---

## 6. Checklist para Novas Janelas / Componentes

Ao adicionar uma nova janela ao plugin:

- [ ] A janela está no projeto `Artificer` (não em `Artificer.UI`)? → pode usar `Dalamud.Bindings.ImGui` diretamente
- [ ] A janela usa `Theme.Push()` em `PreDraw()` e `Theme.Pop()` em `PostDraw()`?
- [ ] Se usa `DrawGuard.Try()` em `Draw()`, as exceções gerenciadas estão protegidas
- [ ] A janela tem `IDisposable` com remoção do `WindowSystem`?

Ao adicionar um novo componente em `Artificer.UI`:

- [ ] Todas as chamadas `PushStyleVar` usam `ImRaii.PushStyle()` (não `ImGui.PushStyleVar` direto)?
- [ ] Todas as chamadas `PopStyleVar` correspondem exatamente ao número de pushes?
- [ ] Não usa nenhum import de `Dalamud.*` (biblioteca sem Dalamud)?
- [ ] Testar no UIStudio antes de testar no jogo

---

## 7. Referências

| Arquivo | Relevância |
|---------|-----------|
| `C:\Users\aleja\DEV\dalamud\imgui\Dalamud.Bindings.ImGui\Generated\Enums\ImGuiStyleVar.cs` | Valores corretos do enum Dalamud |
| `C:\Users\aleja\DEV\dalamud\Dalamud\Interface\Windowing\WindowHost.cs` | Ciclo de vida de janelas e Alpha push/pop |
| `C:\Users\aleja\DEV\dalamud\Dalamud\Game\ClientState\ClientState.cs` | Implementação de IsLoggedIn |
| `Artificer.UI\ImRaiiShim.cs` | Remap de ImGuiStyleVar ImGuiNET → Dalamud |
| `Artificer.UI\Theme.cs` | ConfigureForDalamud() e Push/Pop de estilos |
| `Artificer\Plugin.cs` | Ponto de inicialização (Theme.ConfigureForDalamud) |
| `docs\ref_crash-history.md` (memory) | Histórico completo de crashes e diagnósticos |
