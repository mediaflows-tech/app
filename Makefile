# Makefile — orchestrates the two-stack Terraform deploy.
# Usage:
#   make help           — list targets
#   make deploy         — full first-time deploy from an empty AWS account
#   make plan / apply   — routine main-stack changes (subsequent changes flow via CI)
#
# All targets default to the prod stack. Override TF_ENV to target another env.

SHELL         := bash
.SHELLFLAGS   := -eu -o pipefail -c
.DEFAULT_GOAL := help

# Config
AWS_PROFILE   ?= mediaflows
TF_ENV        ?= prod
BOOTSTRAP_DIR := infrastructure/bootstrap
MAIN_DIR      := infrastructure
VAR_FILE      := environments/$(TF_ENV).tfvars
BACKEND_HCL   := bootstrap/backend.hcl

export AWS_PROFILE

# Export temporary credentials into env vars before each `terraform` call.
# This works for both SSO (`login_session`, `sso_start_url`) and static-key
# profiles, whereas Terraform's built-in AWS_PROFILE support only covers the
# static-key and classic SSO formats.
AWS_EXPORT := eval "$$(aws --profile $(AWS_PROFILE) configure export-credentials --format env)"

# Targets
.PHONY: help
help: ## List targets
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| sort \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2}'

.PHONY: bootstrap
bootstrap: ## Apply the bootstrap stack (state bucket, OIDC, GHA role, SSM)
	@test -f $(BOOTSTRAP_DIR)/terraform.tfvars || { \
		echo >&2 "missing $(BOOTSTRAP_DIR)/terraform.tfvars — copy from terraform.tfvars.example and fill in secrets"; \
		exit 1; \
	}
	$(AWS_EXPORT) && terraform -chdir=$(BOOTSTRAP_DIR) init
	$(AWS_EXPORT) && terraform -chdir=$(BOOTSTRAP_DIR) apply
	$(AWS_EXPORT) && terraform -chdir=$(BOOTSTRAP_DIR) output -raw backend_hcl > $(BOOTSTRAP_DIR)/backend.hcl
	@echo
	@echo "✓ bootstrap applied. backend.hcl written to $(BOOTSTRAP_DIR)/backend.hcl."
	@echo "  Keep it local — it is gitignored (the bucket name embeds your AWS account id)."

.PHONY: gh-secret
gh-secret: ## Push the GitHub Actions AWS_ROLE_ARN secret (needs gh auth)
	@gh auth status >/dev/null 2>&1 || { echo >&2 "run 'gh auth login' first"; exit 1; }
	$(AWS_EXPORT) && gh secret set AWS_ROLE_ARN \
		--body "$$(terraform -chdir=$(BOOTSTRAP_DIR) output -raw github_actions_role_arn)"
	@echo "✓ GitHub secret AWS_ROLE_ARN updated"

.PHONY: init
init: ## Initialize the main stack with the bootstrap-generated backend config
	@test -f $(MAIN_DIR)/$(BACKEND_HCL) || { \
		echo >&2 "missing $(MAIN_DIR)/$(BACKEND_HCL) — run 'make bootstrap' first"; \
		exit 1; \
	}
	$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) init -reconfigure -backend-config=$(BACKEND_HCL)

.PHONY: plan
plan: init ## Plan the main stack
	$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) plan -var-file=$(VAR_FILE)

.PHONY: apply
apply: init ## Apply the main stack
	$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) apply -var-file=$(VAR_FILE)

.PHONY: apply-dns
apply-dns: init ## Apply only the Route53 hosted zone (unblocks registrar NS update)
	$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) apply \
		-var-file=$(VAR_FILE) \
		-target='module.dns[0].aws_route53_zone.main'

.PHONY: ns
ns: ## Print Route53 name servers for registrar update
	@$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) output -json name_servers | python3 -c \
		'import json, sys; ns = json.load(sys.stdin); print("\n".join(ns))'

.PHONY: deploy
deploy: ## First-time deploy: bootstrap → gh-secret → apply-dns → NS pause → apply
	$(MAKE) bootstrap
	$(MAKE) gh-secret
	$(MAKE) apply-dns
	@echo
	@echo "────────────────────────────────────────────────────────────"
	@echo "Route53 name servers (update these at your domain registrar):"
	@echo "────────────────────────────────────────────────────────────"
	@$(MAKE) --no-print-directory ns
	@echo "────────────────────────────────────────────────────────────"
	@echo "Verify propagation with: dig NS example.com @8.8.8.8 +short"
	@echo "────────────────────────────────────────────────────────────"
	@read -r -p "Press ENTER once NS propagation is confirmed... " _
	$(MAKE) apply

.PHONY: destroy
destroy: ## Destroy everything — main then bootstrap. Requires CONFIRM=nuke
ifneq ($(CONFIRM),nuke)
	@echo >&2 "refusing — pass CONFIRM=nuke to proceed"
	@exit 1
endif
	-$(AWS_EXPORT) && terraform -chdir=$(MAIN_DIR) destroy -var-file=$(VAR_FILE)
	@echo "Main stack destroyed. Emptying state bucket and destroying bootstrap..."
	-$(AWS_EXPORT) && BUCKET="$$(terraform -chdir=$(BOOTSTRAP_DIR) output -raw state_bucket_name)"; \
		for sel in Versions DeleteMarkers; do \
			items=$$(aws s3api list-object-versions --bucket "$$BUCKET" --output json \
				--query "{Objects: $$sel[].{Key:Key,VersionId:VersionId}}"); \
			if [ "$$(echo "$$items" | jq '.Objects | length // 0')" -gt 0 ]; then \
				aws s3api delete-objects --bucket "$$BUCKET" --delete "$$items"; \
			fi; \
		done
	$(AWS_EXPORT) && terraform -chdir=$(BOOTSTRAP_DIR) destroy
