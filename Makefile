# CRM Sales - Common tasks

.PHONY: up down build run migrate

## Start infrastructure (Postgres, RabbitMQ, Keycloak)
up:
	docker compose up -d

## Stop infrastructure
down:
	docker compose down

## Build solution
build:
	dotnet build CrmSales.slnx

## Run Aspire AppHost (starts all services)
run:
	dotnet run --project src/AppHost/CrmSales.AppHost/CrmSales.AppHost.csproj

## Run EF migrations for all modules
migrate:
	dotnet ef database update --project src/Modules/Products/CrmSales.Products.Infrastructure --startup-project src/Api/CrmSales.Api
	dotnet ef database update --project src/Modules/Users/CrmSales.Users.Infrastructure --startup-project src/Api/CrmSales.Api
	dotnet ef database update --project src/Modules/Opportunities/CrmSales.Opportunities.Infrastructure --startup-project src/Api/CrmSales.Api
	dotnet ef database update --project src/Modules/Quotes/CrmSales.Quotes.Infrastructure --startup-project src/Api/CrmSales.Api
	dotnet ef database update --project src/Modules/Orders/CrmSales.Orders.Infrastructure --startup-project src/Api/CrmSales.Api

## Generate migrations (usage: make migration NAME=InitialCreate MODULE=Products)
migration:
	dotnet ef migrations add $(NAME) \
		--project src/Modules/$(MODULE)/CrmSales.$(MODULE).Infrastructure \
		--startup-project src/Api/CrmSales.Api \
		--output-dir Persistence/Migrations
