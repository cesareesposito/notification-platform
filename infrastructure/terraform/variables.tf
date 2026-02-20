variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region for Cloud Run and other regional resources"
  type        = string
  default     = "europe-west1"
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "api_image" {
  description = "Docker image for notification-api (e.g. gcr.io/my-project/notification-api:latest)"
  type        = string
}

variable "worker_image" {
  description = "Docker image for notification-worker"
  type        = string
}

variable "rabbitmq_host" {
  description = "RabbitMQ host (CloudAMQP or GCE VM)"
  type        = string
}

variable "rabbitmq_username" {
  description = "RabbitMQ username"
  type        = string
  default     = "guest"
}

variable "rabbitmq_password" {
  description = "RabbitMQ password"
  type        = string
  sensitive   = true
}

variable "sendgrid_api_key" {
  description = "SendGrid API key"
  type        = string
  sensitive   = true
}

variable "api_keys" {
  description = "Comma-separated list of valid X-Api-Key values for the API"
  type        = string
  sensitive   = true
  default     = ""
}

variable "min_instances" {
  description = "Minimum Cloud Run instances"
  type        = number
  default     = 1
}

variable "max_instances" {
  description = "Maximum Cloud Run instances"
  type        = number
  default     = 10
}
