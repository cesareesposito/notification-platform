terraform {
  required_version = ">= 1.6"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
  }

  # Uncomment and configure for remote state
  # backend "gcs" {
  #   bucket = "your-terraform-state-bucket"
  #   prefix = "notification-platform"
  # }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

locals {
  name_prefix = "notification-${var.environment}"
}

# ── Enable required APIs ─────────────────────────────────────────────────────
resource "google_project_service" "services" {
  for_each = toset([
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "cloudresourcemanager.googleapis.com",
    "artifactregistry.googleapis.com"
  ])
  service            = each.value
  disable_on_destroy = false
}

# ── Artifact Registry (container images) ─────────────────────────────────────
resource "google_artifact_registry_repository" "images" {
  location      = var.region
  repository_id = "${local.name_prefix}-images"
  format        = "DOCKER"
  description   = "Notification Platform container images"
  depends_on    = [google_project_service.services]
}

# ── Secret Manager secrets ───────────────────────────────────────────────────
resource "google_secret_manager_secret" "rabbitmq_password" {
  secret_id = "${local.name_prefix}-rabbitmq-password"
  replication { auto {} }
  depends_on = [google_project_service.services]
}

resource "google_secret_manager_secret_version" "rabbitmq_password" {
  secret      = google_secret_manager_secret.rabbitmq_password.id
  secret_data = var.rabbitmq_password
}

resource "google_secret_manager_secret" "sendgrid_api_key" {
  secret_id = "${local.name_prefix}-sendgrid-api-key"
  replication { auto {} }
  depends_on = [google_project_service.services]
}

resource "google_secret_manager_secret_version" "sendgrid_api_key" {
  secret      = google_secret_manager_secret.sendgrid_api_key.id
  secret_data = var.sendgrid_api_key
}

# ── Service Account ───────────────────────────────────────────────────────────
resource "google_service_account" "notification_sa" {
  account_id   = "${local.name_prefix}-sa"
  display_name = "Notification Platform Service Account"
}

resource "google_secret_manager_secret_iam_member" "sa_rabbitmq" {
  secret_id = google_secret_manager_secret.rabbitmq_password.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.notification_sa.email}"
}

resource "google_secret_manager_secret_iam_member" "sa_sendgrid" {
  secret_id = google_secret_manager_secret.sendgrid_api_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.notification_sa.email}"
}

# ── Cloud Run — Notification API ─────────────────────────────────────────────
resource "google_cloud_run_v2_service" "api" {
  name     = "${local.name_prefix}-api"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  depends_on = [google_project_service.services]

  template {
    service_account = google_service_account.notification_sa.email

    scaling {
      min_instance_count = var.min_instances
      max_instance_count = var.max_instances
    }

    containers {
      image = var.api_image

      ports {
        container_port = 8080
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }
      env {
        name  = "RabbitMq__Host"
        value = var.rabbitmq_host
      }
      env {
        name  = "RabbitMq__Username"
        value = var.rabbitmq_username
      }
      env {
        name = "RabbitMq__Password"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.rabbitmq_password.secret_id
            version = "latest"
          }
        }
      }
    }
  }
}

# Allow unauthenticated calls (behind the X-Api-Key middleware)
resource "google_cloud_run_v2_service_iam_member" "api_public" {
  name     = google_cloud_run_v2_service.api.name
  location = google_cloud_run_v2_service.api.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# ── Cloud Run — Notification Worker ──────────────────────────────────────────
resource "google_cloud_run_v2_service" "worker" {
  name     = "${local.name_prefix}-worker"
  location = var.region
  # Worker doesn't accept inbound traffic
  ingress  = "INGRESS_TRAFFIC_INTERNAL_ONLY"

  depends_on = [google_project_service.services]

  template {
    service_account = google_service_account.notification_sa.email

    scaling {
      min_instance_count = 1
      max_instance_count = var.max_instances
    }

    containers {
      image = var.worker_image

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }
      env {
        name  = "RabbitMq__Host"
        value = var.rabbitmq_host
      }
      env {
        name  = "RabbitMq__Username"
        value = var.rabbitmq_username
      }
      env {
        name = "RabbitMq__Password"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.rabbitmq_password.secret_id
            version = "latest"
          }
        }
      }
      env {
        name = "Providers__SendGrid__ApiKey"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.sendgrid_api_key.secret_id
            version = "latest"
          }
        }
      }
      env {
        name  = "Templates__FileSystem__BasePath"
        value = "/app/templates"
      }
    }
  }
}
