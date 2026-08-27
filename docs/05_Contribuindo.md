# Guia Rápido de Contribuição

Buscamos manter o plugin polido, seguro para uso geral sem bans pela Square Enix, e extremamente performático.

## Onde começar

1. **O Backlog:** Cheque `backlog/*.md` (pasta local, gitignored). Bugs conhecidos, feature requests, e itens pendentes da Cosmic Tool e Lista de Coleta estão documentados ali.
2. **A pasta `docs/`** para entender as barreiras e regras de cada componente isolado.
3. **`docs/plugin-health.md`** para incompatibilidades conhecidas com Dalamud/ImGui e gotchas de crash.

## Fluxo ideal (Fork e PR)

1. Fork e pull na sua máquina.
2. Crie a branch seguindo convenções: `fix/crash-recipe-hud`, `feat/new-solver-parameter`.
3. Compile com `.\scripts\build.ps1` e use "DevPlugins" do Dalamud para testar em Debug no jogo.
4. **Obrigatório:**
   - Zero warnings de build (`dotnet build` não deve gerar nenhum warning novo).
   - Execute o test suite e confirme 0 testes quebrados (385 esperados).
   - Se mudou algum arquivo em `Artificer.Simulator/Actions/`, adicione ou atualize o teste correspondente em `Artificer.Test/Simulator/`.
   - Para componentes em `Artificer.UI/`: teste visualmente no UIStudio antes do jogo.
5. Faça commits seguindo Conventional Commits em português (`feat(ui):`, `fix(solver):`, `refactor(simulator):`).
6. Submeta o PR. Aprovações focam em performance do hot path, compatibilidade com Dalamud e ausência de regressões.
7. Release é por CI: o mantenedor faz o push da tag `v*.*.*.*`, que publica automaticamente o GitHub Release + atualiza o `repo.json` (ver `03_Processo_de_Build_e_Deploy.md` §6).

## Regras rápidas

| Área | Regra |
|---|---|
| `Artificer.UI` | Nunca importar `Dalamud.*`. Usar `ImRaii.PushStyle` (não `ImGui.PushStyleVar` direto). |
| Janelas novas | `Theme.Push()` em `PreDraw()`, `Theme.Pop()` em `PostDraw()`. Registrar no `WindowSystem`. |
| Janelas grandes | Dividir em partials por domínio (`Windows/<Janela>.<Domínio>.cs`), como o `MacroEditor.*`. Campos/estado no arquivo shell. |
| Cores | Sempre `Colors.*`. Nunca `new Vector4(r, g, b, 1f)` inline. |
| RAII | Sempre `using var _ = ImRaii.PushColor(...)`. Nunca Push sem Pop correspondente. |
| Testes | MSTest — não NUnit. Framework: `Microsoft.Testing.Platform`. |
