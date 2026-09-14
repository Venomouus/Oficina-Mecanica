import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("render", ROOT / "render.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class RenderTests(unittest.TestCase):
    def setUp(self):
        self.config = json.loads((ROOT / "config.example.json").read_text())

    def test_roles_secrets_and_migration_are_separate(self):
        result = module.render(self.config)
        job = result["migration.json"]["spec"]["template"]["spec"]
        deployment = result["api.json"]["items"][0]["spec"]["template"]["spec"]
        self.assertNotEqual(job["serviceAccountName"], deployment["serviceAccountName"])
        self.assertEqual(["--migrate"], job["containers"][0]["args"])
        self.assertNotIn("/api/config-", json.dumps(job))
        self.assertNotIn("/database/app-", json.dumps(job))
        self.assertNotIn("/database/migrations-", json.dumps(deployment))
        self.assertIn("@sha256:", deployment["containers"][0]["image"])
        for pod in (job, deployment):
            self.assertFalse(pod["automountServiceAccountToken"])
            self.assertEqual(1654, pod["securityContext"]["runAsUser"])
            self.assertTrue(pod["containers"][0]["securityContext"]["readOnlyRootFilesystem"])

    def test_shared_backend_resources_are_not_redefined(self):
        result = module.render(self.config)
        kinds = [item["kind"] for filename in ("api.json", "setup.json") for item in result[filename]["items"]]
        self.assertFalse(set(kinds) & {"Service", "Ingress", "TargetGroupBinding", "Namespace", "Secret"})
        container = result["api.json"]["items"][0]["spec"]["template"]["spec"]["containers"][0]
        self.assertEqual("/health", container["readinessProbe"]["httpGet"]["path"])
        self.assertEqual("/health/live", container["livenessProbe"]["httpGet"]["path"])

    def test_cross_environment_public_ingress_and_mutable_images_are_rejected(self):
        cases = [(["backend", "namespace"], "oficina-producao"), (["gateway", "environment"], "producao"),
                 (["api", "database_name"], "oficina_producao"), (["image_digest"], "latest"),
                 (["alb_subnet_cidrs"], ["0.0.0.0/0", "10.20.16.0/20"]),
                 (["api", "secret_arns", "app"], self.config["api"]["secret_arns"]["migrations"])]
        for path, value in cases:
            config = copy.deepcopy(self.config)
            parent = config
            for key in path[:-1]:
                parent = parent[key]
            parent[path[-1]] = value
            with self.subTest(path=path), self.assertRaises(ValueError):
                module.render(config)


if __name__ == "__main__":
    unittest.main()
