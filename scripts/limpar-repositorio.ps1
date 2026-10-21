# Roda na RAIZ do repositório, depois de extrair o zip da limpeza.
# Faz o que um zip não consegue: apagar arquivos e gerar a solution.

$ErrorActionPreference = "Stop"

Write-Host "`n=== 1. Removendo sobras de template ===" -ForegroundColor Cyan
$sobras = @(git ls-files | Where-Object { $_ -match '(^|/)Class1\.cs$' -or $_ -match '\.http$' })
if ($sobras.Count -eq 0) {
    Write-Host "Nada a remover."
} else {
    $sobras | ForEach-Object { Write-Host "  git rm $_" }
    git rm -q -- $sobras
}

Write-Host "`n=== 2. Criando a solution ===" -ForegroundColor Cyan
if (Get-ChildItem -File -Filter "ECommerceMicroservices.sln*" -ErrorAction SilentlyContinue) {
    Write-Host "Solution já existe, mantendo."
} else {
    # O formato clássico (.sln) é o mais compatível. Em SDKs que não aceitam
    # --format, cai no formato padrão da versão instalada.
    dotnet new sln -n ECommerceMicroservices --format sln 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { dotnet new sln -n ECommerceMicroservices | Out-Null }
}
$projetos = @(git ls-files | Where-Object { $_ -match '\.csproj$' })
dotnet sln add $projetos | Out-Null
Write-Host "$($projetos.Count) projetos adicionados à solution."

Write-Host "`n=== 3. Normalizando quebras de linha (.gitattributes) ===" -ForegroundColor Cyan
git add --renormalize .

Write-Host "`n=== 4. Conferindo que tudo compila ===" -ForegroundColor Cyan
dotnet build -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "O build falhou. Não faça commit antes de resolver." -ForegroundColor Red; exit 1 }

Write-Host "`nPronto. Confira com: git status --short" -ForegroundColor Green
