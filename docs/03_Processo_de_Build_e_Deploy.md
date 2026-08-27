# Processo de Build, Deploy e Empacotamento

## Pré-requisitos

- .NET 10.0 SDK (instalado via Scoop em `C:\Users\aleja\scoop\apps\dotnet-sdk\current\`).
- PowerShell (Windows).
- XIVLauncher/Dalamud configurado em modo desenvolvedor.
- Scripts locais em `scripts/` (gitignored — não estão no repositório, apenas na máquina de desenvolvimento). Contribuidores sem esses scripts usam `dotnet build` / `dotnet test` diretamente.

> **Nota:** `dotnet` via Scoop não está no PATH do Bash. Usar sempre PowerShell com o caminho completo: `"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"`.

---

## 1. Build Local (Iteração de Desenvolvimento)

```powershell
.\scripts\build.ps1
```

Faz restore e gera `.dll` e `.json` em `Artificer/bin/Debug/` (o csproj usa `AppendRuntimeIdentifierToOutputPath=false`, então **não** há subpasta `x64/`). Para deploy automático no Dalamud local:

```powershell
.\scripts\build.ps1 -Deploy
```

**Dica Dalamud:** Nas configurações experimentais do Dalamud, aponte "DevPlugins" para `Artificer\bin\Debug\` para testar via hot-reload sem reinstalar.

---

## 2. Build Release (Empacotamento)

```powershell
.\scripts\build.ps1 -Configuration Release
```

Compila em modo Release e gera o ZIP em `dist/`. O arquivo final (`Artificer-vX.Y.Z.W.zip`) é o artefato de distribuição para o repositório Dalamud.

---

## 3. Gerência de Versão (Bump Versioning)

Nunca altere a versão no `.csproj` manualmente. Use o script dedicado:

```powershell
.\scripts\bump-version.ps1 -Type minor   # feat: X.Y+1.0.0
.\scripts\bump-version.ps1 -Type patch   # fix: X.Y.Z+1.0
.\scripts\bump-version.ps1 -Type build   # chore/refactor: X.Y.Z.W+1
.\scripts\bump-version.ps1 -Type major   # breaking: X+1.0.0.0
```

O script atualiza `Artificer/Artificer.csproj` e exibe `Version bumped: X.Y.Z.W → X.Y.Z.W`. Após o bump, atualizar também a linha de versão no `README.md`.

---

## 4. Rodando Testes

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test
```

Esperado: `Passed! - Failed: 0, Passed: 385, Skipped: 0`

---

## 5. Rodando o UIStudio (visualizador de componentes)

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio
```

Abre uma janela desktop com todas as Stories de componentes — não requer o FFXIV rodando.

---

## 6. CI/CD — Release Automatizado

O push de uma tag `v*.*.*.*` dispara o workflow `.github/workflows/release.yml` (GitHub Actions, `ubuntu-latest`, .NET `10.x`):

1. Baixa o Dalamud (`stg/latest.zip`), faz `dotnet restore` + `dotnet build -c Release` do `Artificer.csproj`.
2. Cria o **GitHub Release** (`softprops/action-gh-release`) com o asset `Artificer/bin/Release/Artificer/latest.zip` (pacote gerado pelo `Dalamud.NET.Sdk`); o nome do release é a própria tag.
3. Atualiza o `repo.json` (`AssemblyVersion` + `LastUpdate`) e o commita/push na `main` como `github-actions[bot]`.

**Fluxo de publicar:** depois de cortar o commit de release + a tag localmente, basta `git push origin main` seguido de `git push origin vX.Y.Z.W`. **Não** criar o release nem editar o `repo.json` na mão — a CI faz. O `repo.json` aponta para o link fixo `releases/latest/download/latest.zip`, então cada tag nova é distribuída automaticamente pelo Plugin Installer.

> Como a CI commita o `repo.json` de volta na `main`, após o push da tag rode `git pull --ff-only` (ou `git fetch` + `git merge --ff-only origin/main`) para trazer esse commit do bot.
