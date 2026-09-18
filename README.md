# 🛒 Microservices E-Commerce Architecture (.NET 9)

Um projeto completo de arquitetura de microsserviços desenvolvido com **.NET 9**, **Docker**, **SQL Server 2022**, **Redis**, **RabbitMQ (MassTransit)** e **YARP API Gateway**. 

Este repositório documenta a implementação de um sistema distribuído de e-commerce moderno, aplicando conceitos de comunicação assíncrona, cache em memória, roteamento de requisições e padrões de resiliência.

---

## 🏗️ Arquitetura do Sistema

O sistema é dividido em microsserviços autônomos e desacoplados, controlados por um API Gateway centralizado:

```text
[ Cliente / Usuário ]
        │
        ▼ (Porta 5094)
[ YARP API Gateway ] ──(Roteamento Inteligente)──┐
        │                                         │
        ├──────────────────────┐                  │
        ▼                      ▼                  ▼
  [ Catalog.API ]       [ Basket.API ]    [ Ordering.API ]
        │                      │                  │
   (SQL Server)          (Redis Cache)     (RabbitMQ + SQL Server)
```

---

## 🚀 Tecnologias Utilizadas

* **Linguagem & Framework:** .NET 9 (C#) / ASP.NET Core Web API
* **Gateway:** YARP (Yet Another Reverse Proxy)
* **Banco de Dados Relacional:** SQL Server 2022 (para Catálogo e Pedidos)
* **Cache em Memória:** Redis (para o Carrinho de Compras)
* **Mensageria Assíncrona:** RabbitMQ com MassTransit
* **Resiliência e Tolerância a Falhas:** Polly (Retry e Circuit Breaker)
* **Documentação de API:** OpenAPI / Scalar
* **Containerização:** Docker & Docker Compose

---

## 📂 Estrutura de Microsserviços

1. **YARP API Gateway (`/ApiGateway`):** Ponto de entrada unificado da aplicação que redireciona o tráfego externo para os microsserviços internos corretos.
2. **Catalog.API (`/Catalog.API`):** Gerencia o catálogo de produtos do e-commerce, conectado a uma base de dados relacional (SQL Server).
3. **Basket.API (`/Basket.API`):** Gerencia o carrinho de compras dos usuários utilizando **Redis** para armazenamento ultrarrápido em memória e publica eventos de checkout.
4. **Ordering.API (`/Ordering.API`):** Consome os eventos de finalização de compra através do **RabbitMQ** e persiste os pedidos de forma permanente no banco de dados.

---

## 🔄 Fluxo de Funcionamento (Exemplo: Checkout)

1. **Adicionar ao Carrinho:** O usuário adiciona produtos ao carrinho através do `Basket.API`, que grava os dados no **Redis**.
2. **Finalização de Pedido (Checkout):** O cliente envia uma requisição de compra.
3. **Mensageria Assíncrona:** O `Basket.API` publica um evento (`BasketCheckoutEvent`) na mensageria do **RabbitMQ**.
4. **Processamento do Pedido:** O `Ordering.API` escuta a fila, consome a mensagem automaticamente de forma assíncrona e salva o pedido no **SQL Server**, garantindo que nenhum dado seja perdido mesmo sob oscilações.

---

## 🛡️ Resiliência com Polly

O sistema conta com políticas avançadas de resiliência configuradas via **Polly**:
* **Retry (Tentativas Automáticas):** Em caso de falhas transitórias de rede ou lentidão na inicialização, a aplicação tenta se reconectar automaticamente (com intervalos exponenciais).
* **Circuit Breaker (Disjuntor):** Protege os microsserviços contra sobrecargas contínuas caso um serviço dependente fique instável.

---

## 👨‍💻 Autor
Desenvolvido por **Bruno Gonçalves dos Santos** como parte de um estudo aprofundado em arquitetura de microsserviços e engenharia de software corporativa.
