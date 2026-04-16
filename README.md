# Notification Platform

Multi-tenant notification platform built with .NET 10. Supports email (SendGrid) and push (Firebase) notifications via RabbitMQ queuing, Scriban templates, PostgreSQL persistence, and Quartz.NET scheduling.

---

## Quick start

```bash
docker compose up          # avvia PostgreSQL + RabbitMQ
dotnet run --project src/Notification.Api
# Swagger UI → http://localhost:8891/api/swagger/index.html
```

Nota operativa: `Notification.Api` e `Notification.Worker` devono condividere lo stesso key ring di ASP.NET Data Protection, altrimenti il worker non riesce a decifrare le password SMTP cifrate dall'API. Nel `docker-compose.yml` questo avviene tramite il volume condiviso `notification-data-protection-keys` montato su `/keys`.

Se hai già salvato password SMTP con un key ring vecchio/non più disponibile, quelle password non sono recuperabili: dopo aver riallineato il key ring devi reinserirle e salvare di nuovo la configurazione tenant.

Autenticazione: header `X-Api-Key` (facoltativo in dev — lascia `ApiAuth.Keys` vuoto per disabilitarlo).

---

## API Reference

### Invio immediato — `/api/notifications`

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `POST` | `/api/notifications/email` | Accoda una notifica email (202 Accepted) |
| `POST` | `/api/notifications/push` | Accoda una notifica push (202 Accepted) |
| `POST` | `/api/notifications/bulk` | Accoda fino a 1 000 notifiche in un'unica chiamata |

**Esempio — email immediata:**
```json
POST /api/notifications/email
{
  "tenantId": "tenant-a",
  "recipient": "mario@example.com",
  "recipientName": "Mario Rossi",
  "templateName": "welcome",
  "language": "it",
  "data": { "product_name": "Acme Pro" }
}
```

**Esempio — bulk:**
```json
POST /api/notifications/bulk
{
  "tenantId": "tenant-a",
  "templateName": "newsletter",
  "channel": "Email",
  "language": "it",
  "sharedData": { "month": "Febbraio" },
  "recipients": [
    { "recipient": "a@example.com", "data": {} },
    { "recipient": "b@example.com", "data": { "promo": "SCONTO10" } }
  ]
}
```

---

### Schedulazione — `/api/scheduled`

#### Invio una tantum

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `POST` | `/api/scheduled/email/once` | Schedula un'email a una data/ora precisa |
| `POST` | `/api/scheduled/push/once` | Schedula una push a una data/ora precisa |

**Esempio — email il 1° marzo 2026 alle 09:00 UTC:**
```json
POST /api/scheduled/email/once
{
  "tenantId": "tenant-a",
  "recipient": "mario@example.com",
  "recipientName": "Mario Rossi",
  "templateName": "promo",
  "language": "it",
  "data": { "promo_code": "SPRING26" },
  "scheduledAt": "2026-03-01T09:00:00Z"
}
```

Risposta `201 Created`:
```json
{ "jobId": "a3f9c1...", "type": "once", "channel": "Email", "tenantId": "tenant-a", "scheduledAt": "2026-03-01T09:00:00+00:00" }
```

---

#### Schedulazione periodica (cron)

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `POST` | `/api/scheduled/email/periodic` | Schedula un'email ricorrente con espressione cron |
| `POST` | `/api/scheduled/push/periodic` | Schedula una push ricorrente con espressione cron |

Il campo `cronExpression` segue il formato **Quartz**: `secondi minuti ore giorno-mese mese giorno-settimana` con **settimo campo `year` opzionale**.

##### Tabella di riferimento cron

| `cronExpression` | Quando scatta |
|-----------------|---------------|
| `"0 0 9 * * ?"` | Ogni giorno alle **09:00** |
| `"0 0 18 * * ?"` | Ogni giorno alle **18:00** |
| `"0 0 9,18 * * ?"` | Ogni giorno alle **09:00 e alle 18:00** |
| `"0 30 8 ? * MON"` | Ogni **lunedì** alle 08:30 |
| `"0 0 9 ? * MON-FRI"` | Ogni **giorno feriale** alle 09:00 |
| `"0 0 9 ? * SUN"` | Ogni **domenica** alle 09:00 |
| `"0 0 9 1 * ?"` | Il **1° di ogni mese** alle 09:00 |
| `"0 0 9 L * ?"` | L'**ultimo giorno del mese** alle 09:00 |
| `"0 0 9 1 1,7 ?"` | Il 1° **gennaio e luglio** alle 09:00 |
| `"0 0/30 * * * ?"` | Ogni **30 minuti** |
| `"0 0/10 8-18 * * ?"` | Ogni 10 minuti **tra le 08:00 e le 18:00** |
| `"0 0 9 ? * 2#1"` | Il **primo martedì del mese** alle 09:00 |
| `"0 0 12 * * ?"` | Ogni giorno alle **12:00** |
| `"0 15 10 ? * *"` | Ogni giorno alle **10:15** |
| `"0 15 10 * * ?"` | Ogni giorno alle **10:15** |
| `"0 15 10 * * ? *"` | Ogni giorno alle **10:15** con campo `year` esplicito |
| `"0 15 10 * * ? 2005"` | Ogni giorno alle **10:15** **solo nel 2005** |
| `"0 * 14 * * ?"` | Ogni minuto **tra le 14:00 e le 14:59** |
| `"0 0/5 14 * * ?"` | Ogni **5 minuti** tra le **14:00 e le 14:55** |
| `"0 0/5 14,18 * * ?"` | Ogni **5 minuti** tra le **14:00 e le 14:55** e tra le **18:00 e le 18:55** |
| `"0 0-5 14 * * ?"` | Ogni minuto **tra le 14:00 e le 14:05** |
| `"0 10,44 14 ? 3 WED"` | A marzo, ogni **mercoledì** alle **14:10 e 14:44** |
| `"0 15 10 ? * MON-FRI"` | Ogni **lunedì-venerdì** alle **10:15** |
| `"0 15 10 15 * ?"` | Il **15 di ogni mese** alle **10:15** |
| `"0 15 10 L * ?"` | L'**ultimo giorno del mese** alle **10:15** |
| `"0 15 10 L-2 * ?"` | Il **penultimo giorno del mese** alle **10:15** |
| `"0 15 10 ? * 6L"` | L'**ultimo venerdì del mese** alle **10:15** |
| `"0 15 10 ? * 6L 2002-2005"` | L'**ultimo venerdì del mese** alle **10:15**, **dal 2002 al 2005** |
| `"0 15 10 ? * 6#3"` | Il **terzo venerdì del mese** alle **10:15** |
| `"0 0 12 1/5 * ?"` | Alle **12:00** ogni **5 giorni**, a partire dal **giorno 1** del mese |
| `"0 11 11 11 11 ?"` | Ogni **11 novembre** alle **11:11** |
| `"H H H * * ?"` | Una volta al giorno a un orario **derivato da hash** |
| `"0 H H(0-7) * * ?"` | Una volta al giorno **tra mezzanotte e le 07:59** a un orario **derivato da hash** |
| `"0 H/15 * * * ?"` | Ogni **15 minuti**, con offset iniziale **derivato da hash** |

> **Nota:** usa `?` nei campi *giorno-mese* o *giorno-settimana* per indicare "nessun vincolo". I due campi non possono essere entrambi specificati contemporaneamente.
>
> **Nota avanzata:** il settimo campo `year` è opzionale. Le espressioni con `H` sono sintassi hash-derived: usale solo se il parser Quartz attivo nella tua versione le supporta.

##### Esempi concreti

**Newsletter settimanale — ogni lunedì alle 09:00:**
```json
POST /api/scheduled/email/periodic
{
  "tenantId": "tenant-a",
  "recipient": "mario@example.com",
  "recipientName": "Mario Rossi",
  "templateName": "newsletter-settimanale",
  "language": "it",
  "data": {},
  "cronExpression": "0 30 8 ? * MON"
}
```

**Reminder giornaliero — ogni giorno alle 18:00, solo fino a fine anno:**
```json
POST /api/scheduled/email/periodic
{
  "tenantId": "tenant-a",
  "recipient": "mario@example.com",
  "templateName": "reminder-serale",
  "language": "it",
  "data": { "action": "Completa il tuo profilo" },
  "cronExpression": "0 0 18 * * ?",
  "endAt": "2026-12-31T23:59:59Z"
}
```

**Digest mensile — il primo del mese alle 09:00, a partire da aprile:**
```json
POST /api/scheduled/email/periodic
{
  "tenantId": "tenant-a",
  "recipient": "mario@example.com",
  "templateName": "digest-mensile",
  "language": "it",
  "data": {},
  "cronExpression": "0 0 9 1 * ?",
  "startAt": "2026-04-01T00:00:00Z"
}
```

Risposta `201 Created`:
```json
{ "jobId": "b7e2a0...", "type": "periodic", "channel": "Email", "tenantId": "tenant-a", "cron": "0 0 9 1 * ?", "startAt": "2026-04-01T00:00:00+00:00", "endAt": null }
```

---

#### Gestione job schedulati

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `GET` | `/api/scheduled` | Lista tutti i job attivi (once + periodic) |
| `GET` | `/api/scheduled/{jobId}` | Dettaglio di un job (nextFireTime, stato, cron, …) |
| `DELETE` | `/api/scheduled/{jobId}` | Cancella e rimuove definitivamente il job |
| `PUT` | `/api/scheduled/{jobId}/pause` | Sospende il job (non scatta finché non viene ripreso) |
| `PUT` | `/api/scheduled/{jobId}/resume` | Riprende un job in pausa |

**Esempio — risposta GET `/api/scheduled`:**
```json
[
  {
    "jobId": "a3f9c1...",
    "type": "once",
    "tenantId": "tenant-a",
    "channel": "Email",
    "recipient": "mario@example.com",
    "templateName": "promo",
    "cron": null,
    "nextFireTime": "2026-03-01T09:00:00+00:00",
    "previousFireTime": null,
    "endAt": null,
    "status": "Normal"
  },
  {
    "jobId": "b7e2a0...",
    "type": "periodic",
    "tenantId": "tenant-a",
    "channel": "Email",
    "recipient": "mario@example.com",
    "templateName": "newsletter-settimanale",
    "cron": "0 30 8 ? * MON",
    "nextFireTime": "2026-03-02T08:30:00+00:00",
    "previousFireTime": null,
    "endAt": null,
    "status": "Normal"
  }
]
```

---

### Amministrazione — `/api/admin`

#### Tenant

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `GET` | `/api/admin/tenants` | Lista tutti i tenant |
| `GET` | `/api/admin/tenants/{tenantId}` | Dettaglio di un tenant |
| `POST` | `/api/admin/tenants/{tenantId}` | Crea un nuovo tenant |
| `PUT` | `/api/admin/tenants/{tenantId}` | Aggiorna la configurazione di un tenant |
| `DELETE` | `/api/admin/tenants/{tenantId}` | Elimina un tenant (e i suoi template) |

#### Template

| Metodo | Endpoint | Descrizione |
|--------|----------|-------------|
| `GET` | `/api/admin/tenants/{tenantId}/templates` | Lista i template di un tenant |
| `POST` | `/api/admin/tenants/{tenantId}/templates` | Crea o aggiorna un template (upsert per nome+canale+lingua) |
| `PUT` | `/api/admin/templates/{id}` | Aggiorna un template per ID numerico |
| `DELETE` | `/api/admin/templates/{id}` | Elimina un template per ID numerico |

---

## Architettura

```
API (Notification.Api)
  │
  ├─ /api/notifications  ──► INotificationQueue ──► RabbitMQ ──► Worker
  │
  └─ /api/scheduled
       └─ Quartz.NET (job store su PostgreSQL)
            └─ PublishNotificationJob ──► INotificationQueue ──► RabbitMQ ──► Worker

Worker (Notification.Worker)
  └─ RabbitMqNotificationConsumer
       └─ NotificationDispatcher
            ├─ ITenantConfigProvider  (PostgreSQL, cache 5 min)
            ├─ ITemplateRepository    (PostgreSQL, cache 10 min)
            ├─ ITemplateRenderer      (Scriban)
            └─ INotificationProvider  (SendGrid | Firebase)
```

### Fallback dei template

Per ogni richiesta il sistema cerca il template nel seguente ordine:
1. `(tenantId, name, channel, language)` — corrispondenza esatta
2. `(tenantId, name, channel, "en")` — fallback lingua inglese
3. `("default", name, channel, language)` — template condiviso
4. `("default", name, channel, "en")` — template condiviso in inglese
