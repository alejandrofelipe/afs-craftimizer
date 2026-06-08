# Craftimizer — Design System

Documentação de referência do vocabulário visual do plugin. Usada pelo agente
`craftimizer-specialist` para tomar decisões de UI consistentes sem precisar
re-derivar padrões do código a cada conversa.

## O que é este Design System

Um conjunto de **tokens, componentes e padrões** derivados diretamente do código do plugin.
Não é uma especificação aspiracional — é uma descrição do que já existe.

Fonte de verdade canônica (código):
- `Craftimizer/Utils/UI/Colors.cs` — todos os tokens de cor
- `Craftimizer/Utils/UI/Theme.cs` — backgrounds, borders, radii aplicados ao ImGui
- `Craftimizer/Utils/UI/ImGuiUtils.cs` — componentes reutilizáveis
- `Craftimizer/Utils/UI/ImGuiUtils.Cosmic.cs` — componentes do Cosmic Tracker

Preview visual interativo:
- `mockup/design-system.html` — abre no browser, mostra todos os tokens e componentes

---

## Documentação por tópico

| Arquivo | Cobre |
|---|---|
| [colors.md](colors.md) | Todos os tokens de cor, valores hex, uso correto |
| [layout.md](layout.md) | Superfícies, espaçamento, border radius, anatomia de janela |
| [components.md](components.md) | Catálogo de componentes com mapeamento para ImGui C# |
| [patterns.md](patterns.md) | Padrões de UI recorrentes no plugin |

---

## Princípios do design

### 1. Dark-first, sempre
Fundo base `#060810` → surface `#0D1120` → elevated `#141928` → overlay `#1B2235`.
Nunca usar fundo branco ou claro. Não existe modo claro.

### 2. Cor = semântica de jogo
Cada cor carrega um significado específico de FFXIV:
- **Verde `#52E5A0`** → Progress (sempre, em todo lugar)
- **Violeta `#B07BFF`** → Quality (sempre)
- **Âmbar `#FFB84A`** → Durability (sempre)
- **Rosa `#FF6C8A`** → CP (sempre)

Nunca usar essas cores para outros fins. Um botão de delete não usa verde porque "ficou bonito".

### 3. Opacidade para hierarquia, não cores novas
Variantes de um estado usam a mesma cor-base com opacidade diferente:
- Badge fill: 14% da cor
- Badge border: 30% da cor
- Highlight de mudança: 12% da cor (`CosmicChanged`)

### 4. Tudo escala com `GlobalScale`
Todo espaçamento, padding e tamanho de fonte no plugin real é multiplicado por
`ImGuiHelpers.GlobalScale`. Nunca hardcode valores absolutos — use as constantes
definidas em `UIConstants` ou multiplique explicitamente.

### 5. Texto muted para hierarquia secundária
Labels de parâmetros, valores em estado idle, texto auxiliar: sempre `Colors.TextMuted`
(`#50607A`). Texto primário sem decoração: `#D0D8E8` (default ImGui).

---

## Quando atualizar o Design System

Rodar `/update-design-system` após qualquer mudança em:
- `Colors.cs` (cor adicionada, renomeada, valor corrigido)
- `Theme.cs` (background, border, radius alterado)
- `ImGuiUtils*.cs` (novo componente público adicionado ou removido)

O comando detecta drift automaticamente e reporta o que mudou.

---

## Adicionando uma nova cor

1. Adicionar o campo em `Colors.cs` na região correta (stat bars, conditions, cosmic, etc.)
2. Documentar com `/// <summary>` o significado semântico
3. Rodar `/update-design-system` para refletir no HTML
4. Não criar tokens CSS avulsos no HTML — todos vêm de `Colors.cs`

## Adicionando um novo componente

1. Implementar o método helper em `ImGuiUtils.cs` (ou `.Cosmic.cs` se for Cosmic-específico)
2. Usar os tokens de `Colors.*` e as constantes de `UIConstants` — sem hardcode
3. Rodar `/update-design-system` para adicionar a seção de demo no HTML
4. Adicionar documentação em [components.md](components.md)
