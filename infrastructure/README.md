# MediaFlows Infrastructure

Terraform that provisions the full AWS estate for MediaFlows, split into a bootstrap stack and the main stack.

> Top-level project README is at [`../README.md`](../README.md).

## Stacks

- `bootstrap/` — local-state stack. Creates the S3 state bucket, GitHub OIDC provider, GitHub Actions IAM role, and SSM SecureString parameters. Run once per AWS account.
- Root (`infrastructure/`) — S3-backed stack. Contains the rest of the cloud setup (VPC, RDS, S3, CloudFront, Cognito, EB, Lambda, Amplify, Route53).

## First-time deploy

Prereqs:

- `aws configure --profile mediaflows` — admin-level IAM user in the target account.
- `gh auth login` — for pushing the GitHub Actions secret (`AWS_ROLE_ARN`).

Steps:

```bash
cp infrastructure/bootstrap/terraform.tfvars.example infrastructure/bootstrap/terraform.tfvars
$EDITOR infrastructure/bootstrap/terraform.tfvars   # fill in the three secrets
make deploy
```

`make deploy`:

1. Applies the bootstrap stack (state bucket, OIDC, GHA role, SSM params).
2. Writes `infrastructure/bootstrap/backend.hcl` with the new state bucket name.
3. Pushes the GHA role ARN to the GitHub repo as `AWS_ROLE_ARN`.
4. Applies `module.dns[0].aws_route53_zone.main` only — creates the Route53 zone so we can hand you the NS records.
5. Prints the four NS records and waits for you to press ENTER once your domain registrar has been updated and propagation is verified (`dig NS <domain> @8.8.8.8`).
6. Applies the full main stack. ACM certs validate, Amplify/EB/Lambda/Cognito come up.

`make deploy` writes `infrastructure/bootstrap/backend.hcl` (the state-bucket backend config). It is **gitignored** — the bucket name embeds your AWS account id, so it is kept local rather than committed. To recreate it without a full deploy, copy `backend.hcl.example` and fill in your bucket name, or re-run `make bootstrap`.

## Subsequent changes

The `terraform-apply.yml` workflow runs `terraform apply` via OIDC on changes under `infrastructure/**`. Its automatic push trigger is disabled for the public release — dispatch it manually from the Actions tab, or run `make apply` locally.

Bootstrap stack changes are rare; when needed, run `make bootstrap` locally.

## Targets

- `make plan` / `make apply` — routine main-stack operations.
- `make ns` — print current Route53 NS records.
- `make destroy CONFIRM=nuke` — full teardown. Destroys main, empties + destroys state bucket, destroys bootstrap.
