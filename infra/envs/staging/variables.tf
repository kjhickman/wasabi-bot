variable "image_digest" {
  description = "Digest of the API container image to deploy (e.g., sha256:abcd...)."
  type        = string

  validation {
    condition     = can(regex("^sha256:[0-9a-fA-F]{64}$", var.image_digest))
    error_message = "image_digest must be a SHA256 digest string (sha256:<64 hex characters>)."
  }
}
