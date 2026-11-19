"""Exercise the operator script with real psql exit codes and session-local fixture tables."""

from pathlib import Path
import subprocess
import sys


script = (Path(__file__).resolve().parents[2] / "docs/reset-two-factor.sql").read_text()
psql = sys.argv[1:] or ["psql"]
fixture = """
CREATE TEMP TABLE "ApplicationUser" ("TenantId" text, "Id" text, "IsDeleted" bool,
    "TwoFactorEnabled" bool, "AuthenticationVersion" int, "SecurityStamp" text,
    "ConcurrencyStamp" text, "AccessFailedCount" int, "LockoutEnd" timestamptz);
INSERT INTO "ApplicationUser" VALUES
    ('verified', 'active', false, true, 4, 's', 'c', 2, now()),
    ('verified', 'deleted', true, true, 4, 's', 'c', 2, now());
CREATE TEMP TABLE "TwoFactorCredentials" ("TenantId" text, "UserId" text, "ProtectedKey" text, "LastAcceptedStep" bigint);
CREATE TEMP TABLE "TwoFactorChallenges" ("TenantId" text, "UserId" text, "IsDeleted" bool, "ProtectedKey" text);
CREATE TEMP TABLE "TwoFactorRecoveryCodes" ("TenantId" text, "UserId" text, "IsDeleted" bool);
CREATE TEMP TABLE "UserRefreshTokens" ("TenantId" text, "UserId" text, "RefreshToken" text, "RefreshTokenExpiryTime" timestamptz);
INSERT INTO "TwoFactorCredentials" VALUES ('verified', 'active', 'seed', 42);
INSERT INTO "TwoFactorChallenges" VALUES ('verified', 'active', false, 'pending');
INSERT INTO "TwoFactorRecoveryCodes" VALUES ('verified', 'active', false);
INSERT INTO "UserRefreshTokens" VALUES ('verified', 'active', 'refresh', now() + interval '1 day');
"""

verify = """
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM "ApplicationUser" WHERE "Id" = 'active' AND NOT "TwoFactorEnabled"
        AND "AuthenticationVersion" = 5 AND "SecurityStamp" <> 's' AND "ConcurrencyStamp" <> 'c'
        AND "AccessFailedCount" = 0 AND "LockoutEnd" IS NULL)
        OR EXISTS (SELECT FROM "TwoFactorCredentials" WHERE "ProtectedKey" IS NOT NULL OR "LastAcceptedStep" <> -1)
        OR EXISTS (SELECT FROM "TwoFactorChallenges" WHERE NOT "IsDeleted" OR "ProtectedKey" IS NOT NULL)
        OR EXISTS (SELECT FROM "TwoFactorRecoveryCodes" WHERE NOT "IsDeleted")
        OR EXISTS (SELECT FROM "UserRefreshTokens" WHERE "RefreshToken" IS NOT NULL OR "RefreshTokenExpiryTime" > now())
    THEN RAISE EXCEPTION 'The intended account or one of its credentials was not reset';
    END IF;
END $$;
"""

for tenant, user, expected in [
    ("verified", "active", 0),
    ("wrong", "active", 3),
    ("verified", "missing", 3),
    ("verified", "deleted", 3),
]:
    result = subprocess.run(
        [*psql, "-Xq", "-v", "ON_ERROR_STOP=1", "-v", f"tenant_id={tenant}", "-v", f"user_id={user}", "-f", "-"],
        input=fixture + script + (verify if expected == 0 else ""), text=True, capture_output=True, check=False,
    )
    if result.returncode != expected:
        raise SystemExit(f"Recovery for {tenant}/{user} exited {result.returncode}, expected {expected}.\n{result.stderr}")
