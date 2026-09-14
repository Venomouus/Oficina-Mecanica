"""Disposable Docker integration: actual database bootstrap + migration command + least-privilege API."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import time
import uuid


def command(args, *, env=None, input=None, required=True):
    result = subprocess.run(args, env=env, input=input, capture_output=True, text=True, encoding="utf-8", timeout=180)
    if required and result.returncode:
        raise RuntimeError("Comando de teste falhou: " + " ".join(args[:3]))
    return result


def main(image, bootstrap_path):
    spec = importlib.util.spec_from_file_location("bootstrap", bootstrap_path / "bootstrap.py")
    bootstrap = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(bootstrap)
    suffix = uuid.uuid4().hex[:12]
    postgres, api = "oficina-deploy-db-" + suffix, "oficina-deploy-api-" + suffix
    passwords = {key: uuid.uuid4().hex + "'!" for key in ("admin", "app", "auth", "migrations")}
    environment = dict(os.environ, POSTGRES_PASSWORD=passwords["admin"])
    containers = [postgres, api]
    try:
        command(["docker", "run", "-d", "--rm", "--name", postgres, "--label", "oficina.deploy-test=" + suffix,
                 "-e", "POSTGRES_PASSWORD", "postgres:16-alpine"], env=environment)
        for _ in range(60):
            if command(["docker", "exec", postgres, "pg_isready", "-h", "127.0.0.1", "-U", "postgres"], required=False).returncode == 0:
                break
            time.sleep(1)
        else:
            raise RuntimeError("PostgreSQL TCP nao ficou pronto")
        contract = {"contract_version": 1, "ssl_mode": "VerifyFull", "address": "127.0.0.1", "port": 5432,
                    "planned_environments": {env: {"branch": branch, "database_name": "oficina_" + env,
                        **{key + "_role": f"oficina_{env}_{key}" for key in ("app", "auth", "migrations")}}
                        for env, branch in (("staging", "develop"), ("producao", "master"))}}
        config = bootstrap.read_contract(contract, "staging")
        runner = bootstrap.Psql(config, "", prefix=["docker", "exec", "-i", "--env", "PGPASSWORD", postgres, "psql"], local_test=True)
        runner.run("postgres", "postgres", passwords["admin"], "CREATE ROLE bootstrap_admin LOGIN CREATEDB CREATEROLE NOSUPERUSER PASSWORD "
                   + bootstrap.literal(bootstrap.scram(passwords["admin"])) + ";")
        bootstrap.prepare(runner, config, "bootstrap_admin", passwords)

        def docker_args(name, role):
            # Passwords passed through inherited env only, never argv/output or files.
            child_env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", Database__MigrateOnStartup="false",
                             ConnectionStrings__DefaultConnection=f"Host=127.0.0.1;Database=oficina_staging;Username=oficina_staging_{role};Password={passwords[role]}")
            args = ["docker", "run", "--rm", "--name", name, "--label", "oficina.deploy-test=" + suffix,
                    "--network", "container:" + postgres, "--read-only", "--tmpfs", "/tmp:rw,nosuid,size=64m",
                    "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--memory", "512m",
                    "-e", "ASPNETCORE_ENVIRONMENT", "-e", "Database__MigrateOnStartup", "-e", "ConnectionStrings__DefaultConnection"]
            return args, child_env

        for index in range(2):
            name = "oficina-deploy-migrate-" + suffix + str(index)
            containers.append(name)
            args, child_env = docker_args(name, "migrations")
            result = command(args + [image, "--migrate"], env=child_env)
            assert "Migrations concluidas" in result.stdout
        print("OK: bootstrap real e migrations idempotentes com role migrations")
        bootstrap.grant_access(runner, config, "bootstrap_admin", passwords)
        try:
            runner.run(config["database_name"], config["app_role"], passwords["app"], 'SELECT * FROM "__EFMigrationsHistory";')
            raise AssertionError("API nao deve acessar historico de migrations")
        except bootstrap.BootstrapError as error:
            assert "42501" in str(error)
        args, child_env = docker_args(api, "app")
        command(args[:2] + ["-d"] + args[2:] + [image], env=child_env)

        def get(path):
            return command(["docker", "exec", postgres, "wget", "-q", "-O", "-", "http://127.0.0.1:8080" + path], required=False)

        for _ in range(60):
            if get("/health").returncode == 0:
                break
            time.sleep(1)
        else:
            raise RuntimeError("API com usuario restrito nao ficou pronta")
        assert get("/health/live").returncode == 0
        uid = command(["docker", "exec", api, "id", "-u"]).stdout.strip()
        assert uid == "1654"
        print("OK: API sem DDL/historico de migrations; uid 1654, filesystem readonly e health 200")
        runner.run(config["database_name"], config["migrations_role"], passwords["migrations"], 'REVOKE SELECT ON "Clientes" FROM oficina_staging_app;')
        assert get("/health").returncode != 0
        assert get("/health/live").returncode == 0
        print("OK: falha de banco/schema retira readiness e preserva liveness")
    finally:
        for name in reversed(containers):
            inspect = command(["docker", "inspect", name], required=False)
            if inspect.returncode == 0 and json.loads(inspect.stdout)[0]["Config"]["Labels"].get("oficina.deploy-test") == suffix:
                command(["docker", "rm", "-f", "-v", name])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", default="oficina-api:eks-test")
    parser.add_argument("--bootstrap-path", type=Path, required=True)
    args = parser.parse_args()
    main(args.image, args.bootstrap_path)
