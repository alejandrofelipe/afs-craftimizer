---
name: version-bump
description: Incrementa a versão do plugin Artificer (MAJOR.MINOR.PATCH.BUILD) via scripts/bump-version.ps1 seguindo conventional commits. Usar para bumpar a versão isoladamente, fora do fluxo de commit completo.
---

# /version-bump

Incrementa a versão do plugin Artificer seguindo convenções semânticas MAJOR.MINOR.PATCH.BUILD.

## Formato: MAJOR.MINOR.PATCH.BUILD

```
2.10.0.2
│ │  │ │
│ │  │ └─ BUILD: refactors/fixes pequenos
│ │  └─── PATCH: bug fixes visíveis ao usuário
│ └────── MINOR: novas features ou update de patch FFXIV
└──────── MAJOR: mudanças incompatíveis
```

## Regras de Incremento

| Tipo de Mudança | Componente | Regra |
|---|---|---|
| Breaking change | MAJOR | Bumpar MAJOR, zerar restante |
| Update patch FFXIV | MINOR | Bumpar MINOR, zerar PATCH/BUILD |
| Nova feature visível | PATCH | Bumpar PATCH, zerar BUILD |
| Bug fix visível | PATCH | Bumpar PATCH, zerar BUILD |
| Refactor/fix pequeno | BUILD | Bumpar apenas BUILD |
| Refactor interno | Nenhum | Não bumpar |

## Mapeamento com Conventional Commits

| Commit Type | Bump |
|---|---|
| `feat(scope): ...` | PATCH (ou MINOR se feature grande) |
| `fix(scope): ...` | PATCH (ou BUILD se trivial) |
| `feat!:` / `BREAKING CHANGE:` | MAJOR |
| `refactor:`, `chore:`, `perf:` | BUILD |
| `docs:`, `style:`, `test:` | Nenhum |

## Procedimento

1. Determinar o tipo de bump baseado nas mudanças realizadas
2. Executar o script:

```powershell
.\scripts\bump-version.ps1 -Type major   # 2.10.0.2 → 3.0.0.0
.\scripts\bump-version.ps1 -Type minor   # 2.10.0.2 → 2.11.0.0
.\scripts\bump-version.ps1 -Type patch   # 2.10.0.2 → 2.10.1.0
.\scripts\bump-version.ps1 -Type build   # 2.10.0.2 → 2.10.0.3
```

3. Incluir a nova versão na mensagem de commit:

```
feat(gear): implementa tracking de desgaste de gear

- Adiciona GearWearTracker.cs
- Configuração opt-in em Settings

Version: 2.10.1.0
Closes #123
```

## Regra importante: Bumpar ANTES do commit final

Não fazer bump em commit separado — o `.csproj` com versão atualizada deve fazer parte do commit da feature/fix.

## Antipatterns

- **❌ Bump pós-commit**: Cria commit extra desnecessário no histórico
- **❌ Não zerar componentes inferiores**: `2.10.0.2 → 2.11.0.2` está errado; correto é `2.11.0.0`
- **❌ Não mencionar versão no commit**: Dificulta rastreamento no `git log`
- **❌ Versões duplicadas**: Sempre bumpar antes de nova release
