# Two-factor authentication

Krafter supports optional TOTP authenticator enrollment for each tenant user. Users manage it under **Account → Two-factor authentication**. Enrollment and login work with both split-host and single-host templates. Passkeys, trusted-device bypass, SMS/email codes and mandatory tenant-wide enrollment are not included.

## User workflow

1. Sign in and open the two-factor settings page.
2. Verify your current password. A Google-only account without a local password must have signed in with Google within the last five minutes.
3. Scan the locally generated QR code with an authenticator, or enter the setup key manually.
4. Enter a code to confirm enrollment. An existing authenticator remains active during replacement until the new authenticator is confirmed.
5. Save the ten recovery codes. They are displayed once and can be downloaded locally. No later endpoint can retrieve them in plaintext.
6. Sign in again. A correct password or Google primary login now starts a separate second-factor step. Use an authenticator code or a single-use recovery code to complete it.

A successfully used authenticator time step cannot be reused. If you just signed in, confirmed enrollment or changed a setting using your authenticator, wait for the next code before using it again. Recovery-code login requires the primary login as well; a recovery code is not a password substitute.

Replacing recovery codes invalidates the entire old batch. Enabling, disabling, replacing an authenticator, or regenerating recovery codes invalidates previous JWT sessions, refresh credentials and pending challenges. Save the new recovery codes before choosing to sign in again. Reloading the page loses that one-time display; if necessary, verify an enrolled factor to regenerate another batch.

Password reset does not disable two-factor authentication. Google login is also subject to Krafter's locally enrolled factor: this implementation does not treat a Google login as proof that Google performed MFA. OAuth state is correlated with the initiating browser session, and callback/return hosts are restricted to the application's configured tenant addresses.

Password login, MFA password proof, and new passwords on change/reset share a maximum of 1,024 characters. Oversized new passwords are rejected before saving. A previously stored password longer than this limit requires a password reset to a supported length, or a password change while the user is already signed in; the current-password field still accepts the old value for that change. Passwords are never truncated. Resetting such a password preserves any enrolled MFA requirement.

TOTP is useful protection against stolen passwords, but is not phishing-resistant. This feature does not make a NIST compliance claim.

## Production key storage

Authenticator seeds are encrypted with ASP.NET Core Data Protection using application-, tenant- and user-specific purposes. Persisted challenge identifiers and high-entropy recovery codes are hashed. Recovery codes contain 128 random bits; neither passwords nor TOTP seeds are derived from them.

Development uses the platform's per-user Data Protection key ring. **Production enrollment is unavailable until both settings below are supplied:**

- `TwoFactor__KeyRingPath`: persistent key-ring directory, shared by backend replicas of the same application.
- `TwoFactor__KeyEncryptionCertificatePath`: mounted PKCS#12 RSA certificate with the private key used to protect/unprotect key-ring material.
- `TwoFactor__KeyEncryptionCertificatePassword`: the certificate password, when needed, supplied through secure deployment configuration rather than a tracked settings file.

The application name is `AditiKraft.Krafter` (renamed when a template app is generated). Replicas must use the same application name, key ring and certificate access. Give only the application identity access to key storage and the certificate. Keep keys/certificate backups separate from a database-only backup; test restoration. Losing the key ring or private key can make existing authenticator enrollments unusable. Do not delete old keys during routine rotation. Certificate replacement requires retaining decryption capability for keys protected by earlier certificates; plan and test rotation with the deployment's Data Protection configuration before removing an old certificate.

Use a unique, securely supplied production JWT signing key on the API and UI hosts; the checked-in example signing keys are development samples.

No seed, setup URI, recovery code, login challenge or credential response should be logged or sent to analytics. Secret-bearing responses use `Cache-Control: no-store`. The QR renderer runs locally and makes no external request. Use HTTPS and preserve certificate verification.

## API and deployment changes

`POST /api/tokens` and `POST /api/external-auth/google` now return `Response<LoginResponse>`. Successful `Data` contains either `Tokens` (the existing completed `TokenResponse`) or `Challenge` (opaque ID plus expiry), never both. The second step, `POST /api/two-factor/login`, returns `Response<TokenResponse>` only after verification succeeds. Update external API clients for this contract change.

Authenticated management routes are under `/api/two-factor`: `GET /status`, `POST /setup`, `POST /confirm`, `POST /disable`, and `POST /recovery-codes`. Mutating two-factor requests require `X-Krafter-Mfa: 1`; browser requests must also have the exact current-host origin. The split-host BFF and typed clients apply these rules. Never relax CORS to arbitrary origins or allow that header from untrusted origins.

Login challenges expire after five minutes and allow at most five verification failures. Per-account Identity failure/lockout state survives new challenges. Request-level throttling supplements the durable account limit. Split-host deployments must configure [trusted proxy addresses](url-configuration.md#trusted-reverse-proxies-and-client-addresses), including the UI server on Backend, to preserve independent client limits. Untrusted forwarding headers cannot select another client's limit. Correct primary authentication does not clear failed second-factor attempts before login finishes. PostgreSQL advisory transaction locks serialize account transitions across API replicas; process-local locks and cache get/remove pairs are not used for one-time proofs.

TOTP verification uses six digits, SHA-1, 30-second periods and a window of one previous/current/one future step. Server clocks must be synchronized. The actually matched time step is consumed atomically, including enrollment confirmation. Recovery codes are consumed in the same transaction as their operation.

The migrator applies the `AddTwoFactorAuthentication` migration before the API starts. Back up the production database and persistent key material before rollout. New tokens contain tenant identity, authentication version, original primary-authentication time/method and MFA assurance. Pre-feature tokens are rejected because they lack the required claims: deployment requires a fresh login. Refresh preserves primary-authentication time rather than manufacturing recent authentication. It selects the account and refresh credential by the verified immutable user ID, so email changes cannot switch the account or transfer its authentication claims.

Account-version checks run on ordinary API requests and on Blazor server authentication in both hosts. Single-host checks use the resolved tenant in-process; split-host checks forward the exact verified token. Password changes and resets save the session version with the password atomically and clear browser authentication before requiring a fresh sign-in. SignalR invocations also revalidate the session. Existing realtime connections recheck account/tenant state every five seconds and close if revoked, expired or validation is unavailable. This gives existing sockets a bounded five-second revalidation interval rather than pretending that an HTTP token-version check physically closes them instantly. Do not use an existing socket alone as proof of recent authentication for a sensitive operation.

MFA setup, confirmation, disabling and recovery-code regeneration revalidate the original request's user, tenant, session version and MFA assurance after acquiring the account lock. This check precedes factor consumption and management changes: a request that passed HTTP authentication before a concurrent revocation cannot resume with its old session afterward, including a recent Google-only session.

Cached server permissions are attached only when their credential matches the exact verified token. Unsigned permission cookies are ignored; other header/query callers use the verified user's database permissions. Invalid response envelopes and temporary authentication-service failures produce structured errors, and unavailable session validation rejects authentication.

Deleting a non-owner account advances its authentication version and rotates its Identity concurrency/security stamps together with the soft deletion. In-flight Identity updates cannot restore the deleted row. Rejected failure-count updates are detached before the MFA transaction finishes; a failed failure-count reset cannot issue credentials.

Krafter's realtime session validation is scoped to its application `RealtimeHub`. TickerQ's dashboard and notification hub retain their existing, separate Basic authentication and configured dashboard credentials.

Consumed recovery records are purged hourly. Challenges older than one day past expiry are purged hourly. Cleanup explicitly bypasses tracked soft-delete handling only for expired/consumed MFA state and never deletes active authenticator credentials.

## Lost authenticator and all recovery codes

There is deliberately no email-only reset endpoint or ordinary administrator bypass. An operator with privileged database access may perform recovery only under the application's verified support process:

1. Open an auditable recovery case. Establish the correct tenant and immutable user ID; email is not globally unique across tenants.
2. Verify identity through a pre-established, independent recovery process appropriate to the deployment. Email access, a password reset, a newly supplied telephone number, or an unverified claim of tenant ownership is insufficient by itself. When identity cannot be established, stop; do not weaken the check to resolve the request.
3. For an owner/last-owner account, require independent operator review and the deployment's established ownership-verification procedure. Do not remove the tenant's last owner as a workaround.
4. Record authorization, evidence reference, approvers, tenant/user IDs and time in the operator audit system. Notify the established account contact of the recovery attempt/change without including secrets.
5. Run the reviewed maintenance script [reset-two-factor.sql](reset-two-factor.sql) using a privileged maintenance identity, against the intended application database. Supply `tenant_id` and `user_id` as psql variables. Never expose this operation through a tenant-admin or anonymous API.
6. Require the user to sign in again and enroll a new authenticator. The script revokes old sessions, refresh credentials, recovery codes and challenges. Preserve the case/audit trail and investigate suspicious recovery activity.

The template cannot establish a real person's identity for an operator; each deployment must define and staff that process before relying on manual recovery. A deployment without such a process should communicate that losing all factors can make the account unrecoverable.

## Verification commands

For an application generated from either template, run:

```bash
dotnet build AditiKraft.Krafter.slnx
dotnet test tests/AditiKraft.Krafter.Tests/AditiKraft.Krafter.Tests.csproj
```

When working in the template source repository, use `dotnet build AditiKraft.Krafter.Dev.slnx` to build both source hosting variants. That solution is source-only and is not included in generated applications.

The normal suite includes MFA behavior and cookie-contract tests. Real PostgreSQL concurrency tests run when `KRAFTER_TEST_POSTGRES` points to a **dedicated test database**, never an application's database. They apply application migrations and create isolated tenant users. Keep that connection string in local/CI secret configuration. Without it, those tests are explicitly skipped rather than being reported as executed.

Both generated templates must also build and run. Browser validation should cover enrollment, the pending login state, wrong-code feedback, authenticator/recovery login, regeneration, old-session rejection, disabling, logout and missing-CSRF-header rejection. Do not capture screenshots of real setup keys or recovery codes in test artifacts.
