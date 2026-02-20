Perché PostgreSQL
JSONB per ProviderSettings: Dictionary<string,string> — indicizzabile nativamente, zero overhead. MySQL ha JSON ma senza la stessa potenza. TEXT illimitato per i template Scriban. Npgsql + EF Core 9 è lo stack .NET più maturo per Postgres.

Cosa è stato aggiunto
Nuovo progetto: Notification.Persistence
File	Ruolo
NotificationDbContext.cs	DbContext con JSONB su ProviderSettings
Entities/TenantEntity.cs	Tabella tenants
Entities/NotificationTemplateEntity.cs	Tabella notification_templates con unique index
Repositories/PostgresTenantConfigProvider.cs	Sostituisce StaticTenantConfigProvider, cache 5 min
Repositories/PostgresTemplateRepository.cs	Sostituisce FileSystemTemplateRepository, fallback identico, cache 10 min
Seeding/DatabaseSeeder.cs	Auto-migrate + seed tenants + import .scriban dal filesystem
Migrations/	Migration EF Core generata e pronta
Nuovo controller: AdminController.cs

GET/POST      /admin/tenants
GET/PUT/DELETE /admin/tenants/{tenantId}
GET/POST      /admin/tenants/{tenantId}/templates
PUT/DELETE    /admin/templates/{id}
Avvio in locale

# 1. Avvia Postgres + RabbitMQ
docker-compose up postgres rabbitmq -d

# 2. Avvia API (migra DB + importa template automaticamente)
cd src/Notification.Api && dotnet run

# 3. Crea un nuovo tenant via API
curl -X POST http://localhost:8080/admin/tenants/tenant-c \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Tenant C","emailProvider":"SendGrid","pushProvider":"Firebase","rateLimitPerMinute":50}'

# 4. Aggiungi un template
curl -X POST http://localhost:8080/admin/tenants/tenant-c/templates \
  -H "Content-Type: application/json" \
  -d '{"templateName":"welcome","channel":0,"language":"en","content":"---\nsubject: Ciao {{ recipient_name }}!\n---\nBenvenuto."}'