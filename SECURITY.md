# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| latest  | :white_check_mark: |

Only the latest version on the `main` branch receives security updates.

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Instead, please report vulnerabilities privately via
[GitHub Security Advisories](https://github.com/krishans1990/lk-fx-dashboard/security/advisories/new).

Include:

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

You can expect an initial response within 7 days. We will work with you
to understand the issue and coordinate a fix before any public disclosure.

## Security Considerations

- The `Security:ApiKey` and `Security:AdminPin` values in `appsettings.json`
  are defaults and **must** be changed before deployment.
- Never commit real secrets. Use environment variables or
  `appsettings.*.local.json` (gitignored) for local overrides.
- The `/api/scrape/trigger` endpoint requires the `X-Api-Key` header.
