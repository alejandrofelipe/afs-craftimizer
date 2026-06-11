# Guia Rápido de Contribuição

Buscamos garantir que o plugin fique polido, seguro para uso geral sem bans pela Square, e extremamente perfomático.

## Onde começar
1. **O Backlog:** Cheque a raiz em `backlog/*.md`. Todos os bugs recentes da expansão, refatorações da UI, issues da versão 7.X ou novos calculos de exploração cósmica ficam ali descritos.
2. Leia a pasta `docs/` recém criada para entender as barreiras e regras dos componentes isolados.

## Fluxo ideal (Fork e PR)
1. Fork e pull na sua máquina.
2. Crie a branch sob convenções (ex: `fix/crash-recipe-hud` ou `feat/new-solver-parameter`).
3. Compile, e use o Dalamud Settings "DevPlugins" para testar sua branch no jogo em *Debug*.
4. **Mandatório:**
   - Execute o `.editorconfig` lint / formatador da IDE para padronização.
   - Execute o Test suite e confirme 0 testes quebrados.
   - Adicione novos testes (em `Artificer.Test`) caso mude um script nas sub-pastas `/Actions/`.
5. Faça seus Commits.
6. Submeta o PR. Aprovamos ou indicamos otimizações baseadas no *hotpath* de memórias alocadas.
