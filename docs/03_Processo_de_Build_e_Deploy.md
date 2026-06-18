# Processo de Build, Deploy e Empacotamento

## Pré-requisitos

- .NET 10.0 SDK (instalado via Scoop em `C:\Users\aleja\scoop\apps\dotnet-sdk\current\`).
- PowerShell (Windows).
- XIVLauncher/Dalamud configurado em modo desenvolvedor.
- Scripts locais em `scripts/` (gitignored — não estão no repositório, apenas na máquina de desenvolvimento).

> **Nota:** `dotnet` via Scoop não está no PATH do Bash. Usar sempre PowerShell com o caminho completo: `"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"`.

---

## 1. Build Local (Iteração de Desenvolvimento)

```powershell
.\scripts\build.ps1
```

Faz restore e gera `.dll` e `.json` em `Artificer/bin/Debug/x64/`. Para deploy automático no Dalamud local:

```powershell
.\scripts\build.ps1 -Deploy
```

**Dica Dalamud:** Nas configurações experimentais do Dalamud, aponte "DevPlugins" para `Artificer\bin\Debug\x64\` para testar via hot-reload sem reinstalar.

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

Esperado: `Passed! - Failed: 0, Passed: 215, Skipped: 0`

---

## 5. Rodando o UIStudio (visualizador de componentes)

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio
```

Abre uma janela desktop com todas as Stories de componentes — não requer o FFXIV rodando.
