output "api_url" {
  description = "Public URL of the Notification API Cloud Run service"
  value       = google_cloud_run_v2_service.api.uri
}

output "worker_name" {
  description = "Cloud Run service name of the Worker"
  value       = google_cloud_run_v2_service.worker.name
}

output "artifact_registry_url" {
  description = "Artifact Registry URL for pushing images"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.images.repository_id}"
}

output "service_account_email" {
  description = "Service account used by both Cloud Run services"
  value       = google_service_account.notification_sa.email
}
