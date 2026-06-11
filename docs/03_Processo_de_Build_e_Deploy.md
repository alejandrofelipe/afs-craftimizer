# Processo de Build, Deploy e Empacotamento

## Prerequisitos
- .NET 10.0 SDK.
- PowerShell (Windows).
- XIVLauncher/Dalamud configurado no PC local em modo de desenvolvedor.

## 1. Build Local (Dev Iteration)
O processo diário baseia-se no script:
```powershell
./scripts/build.ps1
```
Esse script faz um restore (`dotnet restore`) e gera `.dll` e `.json` locais no caminho de output (ex: `Artificer/bin/Debug/`). 
**Dica Dalamud**: Dentro do menu de configurações experimentais do FFXIV Dalamud, adicione o caminho do projeto local (ex: `C:\Users\...\Artificer\Artificer\bin\Debug`) para testar instantaneamente via "DevPlugins" hot-reload.

## 2. Empacotamento (Release ZIP)
Ao finalizar os testes de QA ou Features:
```powershell
./scripts/build-package.ps1
```
Isto dispara o compilador para o mode `Release` e executa um script pós-build que empacota os binários otimizados de todos os projetos, arquivos de Assets, e `.json` do manifest do dalamud. 
O arquivo final (ex: `Artificer-v2.10.1.1.zip`) cai na pasta secreta `dist/`.

## 3. Gerência de Versão Semântica (Bump Versioning)
Não altere a versão do `.csproj` manualmente! Utilize o script dedicado:
```powershell
./scripts/bump-version.ps1 -v "X.Y.Z.W"
```
Ou com atalhos de flag:
- `-M` para Major release.
- `-m` para Minor release.
- `-p` para Patch level fix.
- `-B` para Build sub-patch increment.

Isso previne regressões no json manifest da distribuição para os repositórios oficiais Dalamud e arquivos CSPROJ simultaneamente.
