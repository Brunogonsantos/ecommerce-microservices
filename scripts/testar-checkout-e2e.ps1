# Teste ponta a ponta do checkout, sempre passando pelo Gateway (5094).
# Requer a stack no ar: docker compose up -d --build
#
# Basket e Ordering exigem token: o fluxo usa o admin (que pode agir em nome de
# qualquer usuário) e, no fim, confere que um cliente comum é barrado (401/403).
#
# Cobre: preço vindo do catálogo, CheckoutId gravado, pedido criado UMA vez,
# carrinho apagado e (opcional) reentrega simulada da mesma mensagem no RabbitMQ.

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$gateway = "http://localhost:5094"
$catalog = "$gateway/catalog-api/api/v1/Catalog"
$basket  = "$gateway/basket-api/api/v1/Basket"
$orders  = "$gateway/ordering-api/api/v1/Order"

function Write-Section($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }
function Pass($m) { Write-Host "PASSOU: $m" -ForegroundColor Green }
function Fail($m) { Write-Host "FALHOU: $m" -ForegroundColor Red; $script:falhas++ }
function Skip($m) { Write-Host "IGNORADO: $m" -ForegroundColor Yellow }
function Get-Status($e) { if ($e.Exception.Response) { [int]$e.Exception.Response.StatusCode } else { -1 } }

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

$script:falhas = 0
$script:ultimoJson = ""
$script:authHeaders = @{}

# Lê os pedidos e SEMPRE devolve um array de verdade (0, 1 ou N itens).
# Evita a armadilha do PowerShell 5.1, em que @(Invoke-RestMethod ...) pode
# devolver um array vazio aninhado e fazer "Count -eq 1" dar verdadeiro sem pedido.
function Get-Orders($user) {
    $resp = Invoke-WebRequest -UseBasicParsing -Uri "$orders/$user" -Method Get -Headers $script:authHeaders
    $script:ultimoJson = $resp.Content
    $parsed = $resp.Content | ConvertFrom-Json
    $itens = @(@($parsed) | Where-Object { $null -ne $_ })
    return ,$itens
}
$usuario = "e2e-$(Get-Random -Maximum 999999)"

Wait-ForStack

# 1. Produto (exige admin)
Write-Section "1. Login e criação do produto"
$login = Invoke-RestMethod -Uri "$gateway/api/v1/Auth/login" -Method Post -ContentType "application/json" `
    -Body (@{ userName = "admin"; password = "Admin@123" } | ConvertTo-Json)
$script:authHeaders = @{ Authorization = "Bearer $($login.token)" }
$produto = Invoke-RestMethod -Uri $catalog -Method Post -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $($login.token)" } `
    -Body (@{ name = "Produto E2E"; category = "Teste"; description = "e2e"; imageFile = ""; price = 800 } | ConvertTo-Json)
Write-Host "Produto $($produto.id) criado com preço $($produto.price)"

# 2. Carrinho com preço adulterado
Write-Section "2. Montando o carrinho (cliente tenta mandar preço 1)"
$carrinho = Invoke-RestMethod -Uri $basket -Method Post -ContentType "application/json" -Headers $script:authHeaders -Body (@{
    userName = $usuario
    items    = @(@{ productId = $produto.id; productName = "x"; price = 1; quantity = 2; color = "preto" })
} | ConvertTo-Json -Depth 5)
if ([decimal]$carrinho.items[0].price -eq 800) { Pass "preço do carrinho veio do catálogo (800)." }
else { Fail "preço no carrinho é $($carrinho.items[0].price), esperado 800." }
if ($null -eq $carrinho.pendingCheckoutId) { Pass "carrinho novo ainda não tem CheckoutId pendente." }
else { Fail "carrinho novo já veio com pendingCheckoutId." }

# 3. Checkout
Write-Section "3. Checkout (esperado: 202 e um CheckoutId)"
$evento = Invoke-RestMethod -Uri "$basket/Checkout" -Method Post -ContentType "application/json" -Headers $script:authHeaders -Body (@{
    userName = $usuario; firstName = "Teste"; lastName = "E2E"; emailAddress = "e2e@teste.com"
    addressLine = "Rua Teste, 1"; country = "Brasil"; state = "RS"; zipCode = "90000-000"
} | ConvertTo-Json)
$checkoutId = $evento.checkoutId
if ($checkoutId -and $checkoutId -ne "00000000-0000-0000-0000-000000000000") { Pass "checkout aceito. CheckoutId: $checkoutId" }
else { Fail "resposta sem CheckoutId válido." }

# 4. Pedido criado exatamente uma vez
Write-Section "4. Aguardando o Ordering.API consumir o evento"
$lista = @()
for ($i = 0; $i -lt 20; $i++) {
    try { $lista = Get-Orders $usuario } catch { $lista = @() }
    if ($lista.Count -ge 1) { break }
    Start-Sleep -Seconds 1
}
Write-Host "Resposta bruta do Ordering.API: $script:ultimoJson"
if ($lista.Count -eq 1) { Pass "exatamente 1 pedido criado para $usuario." }
else { Fail "$($lista.Count) pedido(s) encontrado(s), esperado 1." }
if ($lista.Count -ge 1 -and $lista[0].checkoutId -eq $checkoutId) { Pass "o pedido carrega o mesmo CheckoutId do checkout." }
elseif ($lista.Count -ge 1) { Fail "CheckoutId do pedido ($($lista[0].checkoutId)) difere do checkout ($checkoutId)." }
if ($lista.Count -ge 1 -and [decimal]$lista[0].totalPrice -eq 1600) { Pass "total do pedido = 1600 (2 x 800)." }
elseif ($lista.Count -ge 1) { Fail "total do pedido é $($lista[0].totalPrice), esperado 1600." }

# 5. Carrinho apagado
Write-Section "5. Carrinho apagado após o checkout"
$depois = Invoke-RestMethod -Uri "$basket/$usuario" -Method Get -Headers $script:authHeaders
if (@($depois.items).Count -eq 0) { Pass "carrinho vazio após o checkout." }
else { Fail "carrinho ainda tem itens." }

# 6. Reentrega simulada da MESMA mensagem direto no RabbitMQ
Write-Section "6. Reentrega simulada da mesma mensagem (esperado: continua 1 pedido)"
try {
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("guest:guest"))
    $envelope = @{
        messageType = @("urn:message:EventBus.Messages.Events:BasketCheckoutEvent")
        message     = @{
            checkoutId = $checkoutId; userName = $usuario; totalPrice = 1600
            firstName = "Teste"; lastName = "E2E"; emailAddress = "e2e@teste.com"
            addressLine = "Rua Teste, 1"; country = "Brasil"; state = "RS"; zipCode = "90000-000"
        }
    } | ConvertTo-Json -Depth 5 -Compress
    $corpo = @{
        properties = @{ content_type = "application/vnd.masstransit+json" }
        routing_key = ""; payload = $envelope; payload_encoding = "string"
    } | ConvertTo-Json -Depth 5
    $exchange = [Uri]::EscapeDataString("EventBus.Messages.Events:BasketCheckoutEvent")
    $pub = Invoke-RestMethod -Uri "http://localhost:15672/api/exchanges/%2F/$exchange/publish" -Method Post `
        -Headers @{ Authorization = "Basic $cred" } -ContentType "application/json" -Body $corpo
    if ($pub.routed) {
        Start-Sleep -Seconds 5
        $apos = Get-Orders $usuario
        if ($apos.Count -eq 1) { Pass "mensagem duplicada descartada: continua 1 pedido." }
        else { Fail "$($apos.Count) pedidos após a reentrega, esperado 1." }
    } else {
        Skip "o RabbitMQ não roteou a mensagem simulada (nome do exchange diferente do esperado). Veja os exchanges em http://localhost:15672."
    }
} catch {
    Skip "não foi possível simular a reentrega: $($_.Exception.Message)"
}

# 7. Controle de acesso no Basket e no Ordering
Write-Section "7. Controle de acesso (JWT no Basket.API e no Ordering.API)"
$cliente = Invoke-RestMethod -Uri "$gateway/api/v1/Auth/login" -Method Post -ContentType "application/json" `
    -Body (@{ userName = "cliente"; password = "Cliente@123" } | ConvertTo-Json)
$hCliente = @{ Authorization = "Bearer $($cliente.token)" }

function Testar-Acesso($descricao, $uri, $headers, $esperado) {
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $uri -Method Get -Headers $headers | Out-Null
        $status = 200
    } catch {
        $status = Get-Status $_
    }
    if ($status -eq $esperado) { Pass "$descricao -> $status" }
    else { Fail "$descricao -> $status (esperado $esperado)" }
}

Testar-Acesso "carrinho sem token"                    "$basket/$usuario"  @{}       401
Testar-Acesso "pedidos sem token"                     "$orders/$usuario"  @{}       401
Testar-Acesso "cliente lendo carrinho de outro"       "$basket/$usuario"  $hCliente 403
Testar-Acesso "cliente lendo pedidos de outro"        "$orders/$usuario"  $hCliente 403
Testar-Acesso "cliente lendo o PRÓPRIO carrinho"      "$basket/cliente"   $hCliente 200
Testar-Acesso "cliente lendo os PRÓPRIOS pedidos"     "$orders/cliente"   $hCliente 200
Testar-Acesso "admin lendo pedidos de outro usuário"  "$orders/$usuario"  $script:authHeaders 200

Write-Host ""
if ($script:falhas -eq 0) { Write-Host "Teste concluído sem falhas." -ForegroundColor Green }
else { Write-Host "Teste concluído com $($script:falhas) falha(s)." -ForegroundColor Red }
