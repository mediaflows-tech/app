# infrastructure/environments/dev.tfvars
environment          = "dev"
db_instance_class    = "db.t3.micro"
db_allocated_storage = 20
eb_instance_type     = "t3.micro"
eb_min_instances     = 1
eb_max_instances     = 1
lambda_memory_size   = 256
lambda_timeout       = 60

cognito_callback_urls = [
  "http://localhost:5000/signin-oidc",
  "https://localhost:5001/signin-oidc"
]

cognito_logout_urls = [
  "http://localhost:5000/signout-callback-oidc",
  "https://localhost:5001/signout-callback-oidc"
]
