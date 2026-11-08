-- Privileged operator maintenance only. Follow docs/two-factor-authentication.md.
-- psql "$OPERATOR_DATABASE_URL" -v tenant_id='<verified tenant ID>' -v user_id='<verified user ID>' -f docs/reset-two-factor.sql
-- Record independent recovery approval in the operator audit system BEFORE running.
\set ON_ERROR_STOP on
BEGIN;
SELECT count(*) = 1 AS recovery_target_exists
FROM "ApplicationUser" WHERE "TenantId" = :'tenant_id' AND "Id" = :'user_id' AND NOT "IsDeleted"
\gset
\if :recovery_target_exists
SELECT pg_advisory_xact_lock(hashtextextended('mfa:' || :'tenant_id' || ':' || :'user_id', 0));
UPDATE "ApplicationUser"
SET "TwoFactorEnabled" = false,
    "AuthenticationVersion" = "AuthenticationVersion" + 1,
    "SecurityStamp" = gen_random_uuid()::text,
    "ConcurrencyStamp" = gen_random_uuid()::text,
    "AccessFailedCount" = 0, "LockoutEnd" = NULL
WHERE "TenantId" = :'tenant_id' AND "Id" = :'user_id' AND NOT "IsDeleted";
UPDATE "TwoFactorCredentials" SET "ProtectedKey" = NULL, "LastAcceptedStep" = -1
WHERE "TenantId" = :'tenant_id' AND "UserId" = :'user_id';
UPDATE "TwoFactorChallenges" SET "IsDeleted" = true, "ProtectedKey" = NULL
WHERE "TenantId" = :'tenant_id' AND "UserId" = :'user_id';
UPDATE "TwoFactorRecoveryCodes" SET "IsDeleted" = true
WHERE "TenantId" = :'tenant_id' AND "UserId" = :'user_id';
UPDATE "UserRefreshTokens" SET "RefreshToken" = NULL, "RefreshTokenExpiryTime" = CURRENT_TIMESTAMP
WHERE "TenantId" = :'tenant_id' AND "UserId" = :'user_id';
COMMIT;
\else
ROLLBACK;
\echo 'Recovery aborted: no unique active user matches the verified tenant/user IDs.'
\quit 1
\endif
