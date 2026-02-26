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