# 🛒 E-Commerce Microservices (.NET 9)

[![CI](https://github.com/Brunogonsantos/ecommerce-microservices/actions/workflows/ci.yml/badge.svg)](https://github.com/Brunogonsantos/ecommerce-microservices/actions/workflows/ci.yml)

Projeto de estudo de arquitetura de microsserviços com **.NET 9**, **SQL Server 2022**, **Redis**, **RabbitMQ (MassTransit)**, **YARP** e **Docker Compose**. O foco não é só dividir em serviços, e sim tratar os problemas que aparecem quando eles conversam entre si: mensagens reentregues, dependências fora do ar, banco ainda subindo e dados que o cliente não deve controlar.

---

## 🏗️ Arquitetura

```text
[ Cliente ]
     │
     ▼  :5094
[ YARP API Gateway + endpoint de login (JWT) ]
     │
     ├── /catalog-api/*  ──▶ Catalog.API  :5070 ──▶ SQL Server (CatalogDb)
     ├── /basket-api/*   ──▶ Basket.API   :5034 ──▶ Redis
     │                          │  ▲
     │                          │  └── valida produto e preço (HTTP + Polly) ── Catalog.API
     │                          ▼
     │                      RabbitMQ  (BasketCheckoutEvent)
     │                          │
     └── /ordering-api/* ──▶ Ordering.API :5114 ──▶ SQL Server (OrderingDb)
```

| Serviço | Responsabilidade | Armazenamento |
|---|---|---|
| **ApiGateway** | Entrada única (YARP) e emissão de tokens JWT em `POST /api/v1/Auth/login` | – |
| **Catalog.API** | Catálogo de produtos | SQL Server |
| **Basket.API** | Carrinho, validação de preço/produto e publicação do checkout | Redis |
| **Ordering.API** | Consome o checkout e persiste o pedido (Clean Architecture: Domain / Application / Infrastructure / API) | SQL Server |

Catalog e Ordering usam a mesma instância de SQL Server com bancos lógicos separados (`CatalogDb` e `OrderingDb`). É uma simplificação para ambiente local; em produção, cada serviço teria a sua instância.

---

## ▶️ Como rodar

**Pré-requisitos:** Docker com Docker Compose. A imagem do SQL Server 2022 só tem build `amd64`, então em Macs com Apple Silicon ela depende de emulação.

```bash
docker compose up -d --build
```

No primeiro boot o SQL Server leva de 1 a 2 minutos para ficar pronto. Os serviços que dependem dele tentam de novo automaticamente (veja *Resiliência*) e criam os bancos sozinhos.

| O quê | Endereço |
|---|---|
| Gateway | http://localhost:5094 |
| Catalog.API (Scalar) | http://localhost:5070/scalar/v1 |
| Basket.API (Scalar) | http://localhost:5034/scalar/v1 |
| Ordering.API | http://localhost:5114 |
| RabbitMQ Management | http://localhost:15672 (`guest` / `guest`) |

Para zerar tudo, inclusive os bancos: `docker compose down -v`.

---

## 🔐 Autenticação e autorização

O Gateway emite um JWT (HMAC-SHA256, 60 min) e o **Catalog.API** valida o token.

| Usuário | Senha | Papel |
|---|---|---|
| `admin` | `Admin@123` | `Admin` |
| `cliente` | `Cliente@123` | `Customer` |

```bash
# 1. Login
POST http://localhost:5094/api/v1/Auth/login
{ "userName": "admin", "password": "Admin@123" }

# 2. Criar produto (exige papel Admin)
POST http://localhost:5070/api/v1/Catalog
Authorization: Bearer <token>
```

| Situação | Resultado |
|---|---|
| `POST /api/v1/Catalog` sem token | `401` |
| Mesmo `POST` com token de `cliente` | `403` |
| Mesmo `POST` com token de `admin` | `201` |
| `GET /api/v1/Catalog` (listar/consultar) | público |

> ⚠️ Os usuários são uma lista fixa em memória (com senha em hash), criada só para demonstrar o fluxo. A chave do JWT está no `docker-compose.yml`. Nada disso serve para produção: seria necessário um cadastro de usuários persistido e a chave em um cofre de segredos.

---

## 🔄 Fluxo de checkout

1. `POST /api/v1/Basket` grava o carrinho no Redis. Antes de salvar, o Basket.API consulta cada produto no Catalog.API e **usa o nome e o preço do catálogo**, ignorando o que o cliente enviou. Produto inexistente retorna `400`.
2. `POST /api/v1/Basket/Checkout` gera um `CheckoutId` único, publica o `BasketCheckoutEvent` no RabbitMQ e apaga o carrinho.
3. O Ordering.API consome o evento e cria o pedido, **uma única vez por `CheckoutId`**.

---

## 🛡️ Resiliência e consistência

| Problema | Como é tratado |
|---|---|
| SQL Server ainda subindo | Catalog.API e Ordering.API repetem a criação do banco com backoff exponencial (Polly, 5 tentativas) |
| Catalog.API instável ou fora do ar | Basket.API usa HttpClient com retry e circuit breaker (Polly); com o circuito aberto ou o catálogo inacessível, responde `503` em vez de aceitar um preço não verificado |
| Mensagem reentregue pelo broker | O MassTransit tenta reentregar até 3 vezes. O consumer consulta o `CheckoutId` antes de criar o pedido e há um **índice único** em `Order.CheckoutId` como garantia final; uma violação do índice é tratada como no-op |
| Cliente manipulando o preço | O preço sempre vem do catálogo |

---

## 🧪 Testes e CI

```bash
dotnet test tests/Ordering.UnitTests/Ordering.UnitTests.csproj
dotnet test tests/Basket.UnitTests/Basket.UnitTests.csproj
```

- **14 testes de unidade** (xUnit + Moq + EF Core InMemory): consumer idempotente, mapeamento do evento, validação de preço/produto no carrinho, respostas `400`/`503`, geração do `CheckoutId`.
- **GitHub Actions** (`.github/workflows/ci.yml`): compila os quatro serviços, roda os testes e executa `docker compose build` a cada push e pull request.

---

## 🚧 Limitações conhecidas

Esta seção lista o que **não** está resolvido, de forma intencional.

- **Sem Transactional Outbox.** O checkout publica o evento e depois apaga o carrinho. Se o broker falhar no meio do caminho, o evento pode se perder.
- **Idempotência só no nível da mensageria.** Dois cliques do usuário geram dois `CheckoutId` diferentes e, portanto, dois pedidos.
- **Sem controle de estoque.** Nada impede vender acima do disponível.
- **Só o Catalog.API valida o JWT.** Os endpoints do Basket.API e do Ordering.API ainda são abertos.
- **Sem observabilidade.** Não há tracing distribuído nem logs centralizados.
- **Schema com `EnsureCreated()`.** Não há migrations; mudar o modelo exige recriar o banco.
- **`depends_on` sem healthcheck.** A ordem de subida é compensada por retry no código, não pelo Compose.
- **Testes de integração ausentes.** O teste de unidade só confirma que o índice único está declarado no modelo; a rejeição real de duplicatas pelo SQL Server não é exercitada automaticamente.

---

## 👨‍💻 Autor

Desenvolvido por **Bruno Gonçalves dos Santos** como estudo aprofundado de arquitetura de microsserviços.
