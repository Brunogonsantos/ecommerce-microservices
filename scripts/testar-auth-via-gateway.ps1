# Testa o fluxo de autenticação passando SEMPRE pelo Gateway (porta 5094),
# usando as rotas /catalog-api/* em vez de chamar o Catalog.API direto (5070).

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$gateway = "http://localhost:5094"
$catalogViaGateway = "$gateway/catalog-api/api/v1/Catalog"

function Write-Section($titulo) { Write-Host "`n=== $titulo ===" -ForegroundColor Cyan }
function Test-Resultado($ok, $msgOk, $msgFalha) {
    if ($ok) { Write-Host "PASSOU: $msgOk" -ForegroundColor Green }
    else     { Write-Host "FALHOU: $msgFalha" -ForegroundColor Red }
}
function Get-Status($erro) {
    if ($erro.Exception.Response) { return [int]$erro.Exception.Response.StatusCode } else { return -1 }
}

# Aguarda a stack responder. "docker compose up -d" volta assim que os containers
# iniciam, mas Catalog e Ordering só começam a escutar depois de criar o banco
# (com retry), o que pode levar quase um minuto. Qualquer status abaixo de 500
# conta como "no ar" (um 401 de endpoint protegido, por exemplo).
function Wait-ForStack {
    $alvos = @(
        @{ Nome = "Catalog (via Gateway)";  Uri = "$gateway/catalog-api/api/v1/Catalog" },
        @{ Nome = "Basket (via Gateway)";   Uri = "$gateway/basket-api/api/v1/Basket/x" },
        @{ Nome = "Ordering (via Gateway)"; Uri = "$gateway/ordering-api/api/v1/Order/x" }
    )
    $limite = (Get-Date).AddMinutes(3)
    Write-Host "Aguardando a stack ficar pronta (até 3 min)..." -ForegroundColor Cyan
    foreach ($alvo in $alvos) {
        while ($true) {
            try { Invoke-WebRequest -UseBasicParsing -Uri $alvo.Uri -Method Get | Out-Null; $status = 200 }
            catch { $status = Get-Status $_ }
            if ($status -ge 200 -and $status -lt 500) {
                Write-Host "  $($alvo.Nome): pronto ($status)" -ForegroundColor Green
                break
            }
            if ((Get-Date) -gt $limite) {
                Write-Host "  $($alvo.Nome): sem resposta a tempo (último status $status). Veja: docker compose logs" -ForegroundColor Red
                exit 1
            }
            Start-Sleep -Seconds 3
        }
    }
}

$produtoBody = @{
    name        = "Produto via Gateway $(Get-Random -Maximum 9999)"
    category    = "Teste"
    description = "Criado passando pelo Gateway"
    imageFile   = ""
    price       = 1500
} | ConvertTo-Json

Wait-ForStack

# 1. Login (o endpoint de login vive no próprio Gateway)
Write-Section "1. Login como admin (Gateway)"
$admin = Invoke-RestMethod -Uri "$gateway/api/v1/Auth/login" -Method Post -ContentType "application/json" `
    -Body (@{ userName = "admin"; password = "Admin@123" } | ConvertTo-Json)
Write-Host "Token recebido para $($admin.userName) (papel: $($admin.role))" -ForegroundColor Green

# 2. Sem token -> 401
Write-Section "2. POST via Gateway SEM token (esperado: 401)"
try {
    Invoke-RestMethod -Uri $catalogViaGateway -Method Post -ContentType "application/json" -Body $produtoBody | Out-Null
    Test-Resultado $false "" "a requisição passou sem token."
} catch {
    $s = Get-Status $_
    Test-Resultado ($s -eq 401) "rejeitado com 401 passando pelo Gateway." "status $s (esperado 401). Se for 404, a rota /catalog-api não está chegando no Catalog.API."
}

# 3. Token de admin -> 201 (prova que o header Authorization atravessa o proxy)
Write-Section "3. POST via Gateway COM token de admin (esperado: 201)"
$produto = $null
try {
    $produto = Invoke-RestMethod -Uri $catalogViaGateway -Method Post -ContentType "application/json" `
        -Headers @{ Authorization = "Bearer $($admin.token)" } -Body $produtoBody
    Test-Resultado ($null -ne $produto.id) "criado via Gateway. Id: $($produto.id)" "resposta sem id."
} catch {
    Test-Resultado $false "" "status $(Get-Status $_). O header Authorization pode não estar atravessando o proxy."
}

# 4. Token de cliente -> 403
Write-Section "4. POST via Gateway com token de CLIENTE (esperado: 403)"
$cliente = Invoke-RestMethod -Uri "$gateway/api/v1/Auth/login" -Method Post -ContentType "application/json" `
    -Body (@{ userName = "cliente"; password = "Cliente@123" } | ConvertTo-Json)
try {
    Invoke-RestMethod -Uri $catalogViaGateway -Method Post -ContentType "application/json" `
        -Headers @{ Authorization = "Bearer $($cliente.token)" } -Body $produtoBody | Out-Null
    Test-Resultado $false "" "cliente conseguiu criar produto."
} catch {
    $s = Get-Status $_
    Test-Resultado ($s -eq 403) "cliente rejeitado com 403 via Gateway." "status $s (esperado 403)."
}

# 5. Token inválido -> 401
Write-Section "5. POST via Gateway com token ADULTERADO (esperado: 401)"
try {
    Invoke-RestMethod -Uri $catalogViaGateway -Method Post -ContentType "application/json" `
        -Headers @{ Authorization = "Bearer $($admin.token)x" } -Body $produtoBody | Out-Null
    Test-Resultado $false "" "token adulterado foi aceito."
} catch {
    $s = Get-Status $_
    Test-Resultado ($s -eq 401) "token adulterado rejeitado com 401." "status $s (esperado 401)."
}

# 6. GETs públicos via Gateway
Write-Section "6. GET público via Gateway (esperado: 200)"
try {
    $lista = Invoke-RestMethod -Uri $catalogViaGateway -Method Get
    Test-Resultado (@($lista).Count -ge 1) "listagem pública OK ($(@($lista).Count) produto(s))." "listagem vazia."
    if ($produto) {
        $um = Invoke-RestMethod -Uri "$catalogViaGateway/$($produto.id)" -Method Get
        Test-Resultado ($um.id -eq $produto.id) "consulta por id OK via Gateway." "id retornado diferente."
    }
} catch {
    Test-Resultado $false "" "GET falhou: status $(Get-Status $_)"
}

Write-Host "`nTeste concluído." -ForegroundColor Cyan
