"""Build the versioned Grafana dashboard; labels contain no customer information."""
import json
from pathlib import Path

panels = []


def panel(title, expression, x, y, width=12, height=7, kind="timeseries", description="", unit="short"):
    panels.append({
        "id": len(panels) + 1, "title": title, "type": kind, "description": description,
        "gridPos": {"x": x, "y": y, "w": width, "h": height},
        "datasource": {"type": "prometheus", "uid": "oficina-prom"},
        "targets": [{"refId": "A", "expr": expression, "legendFormat": "{{status}}{{route}}"}],
        "fieldConfig": {"defaults": {"unit": unit}, "overrides": []},
        "options": {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}},
    })


panel("OS criadas hoje (UTC)", "max(oficina_os_today)", 0, 0, 6, 4, "stat",
      "Contagem real do banco desde 00:00 UTC. Grafico diario abaixo preserva dias anteriores enquanto houver dados locais.")
panel("Coleta do banco OK (1=sim)", "max(oficina_business_collection_ok)", 6, 0, 6, 4, "stat")
panel("Respostas 5xx de OS / 5 minutos", "sum(increase(oficina_os_failures_total[5m])) or vector(0)", 12, 0, 6, 4, "stat")
panel("Idade da ultima coleta", "time() - max(oficina_business_last_success_unixtime)", 18, 0, 6, 4, "stat", unit="s")
panel("Latencia p95 por rota", "histogram_quantile(0.95, sum by (le, route) (rate(oficina_http_duration_seconds_bucket[5m])))", 0, 4, unit="s")
panel("Requisicoes por segundo", "sum by (route) (rate(oficina_http_requests_total[5m]))", 12, 4, unit="reqps")
panel("Tempo medio por status — periodos encerrados / 24h", "oficina_os_status_duration_seconds", 0, 11, description=
      "Somente periodos reais com inicio e fim conhecidos, encerrados nas ultimas 24h. Sem amostras = sem dados. Finalizada representa o periodo entre finalizacao e entrega.", unit="s")
panel("Quantidade de amostras por status / 24h", "oficina_os_status_samples", 12, 11)
panel("Volume de OS por dia UTC — ultimos 7 dias", "oficina_os_daily", 0, 18, kind="table", description=
      "Contagem por data de criacao real no banco (UTC), nao por janela movel. Dias sem registros podem nao aparecer.")
panels[-1]["targets"][0].update(instant=True, format="table")
panel("Falhas HTTP por rota (5xx)", 'sum by (route) (rate(oficina_http_requests_total{status_code=~"5.."}[5m]))', 12, 18)
panel("Memoria do processo API", "oficina_process_memory_bytes", 0, 25, description="Processo .NET em Docker; nao representa memoria total de um cluster Kubernetes.", unit="bytes")
panel("CPU do processo API (cores)", "rate(oficina_process_cpu_seconds_total[1m])", 12, 25, description="Tempo de CPU do processo; nao substitui metricas dos nodes/pods de Kubernetes.")
panels.append({"id": len(panels) + 1, "title": "Logs correlacionados da API", "type": "logs",
               "gridPos": {"x": 0, "y": 32, "w": 24, "h": 9},
               "datasource": {"type": "loki", "uid": "oficina-loki"},
               "targets": [{"refId": "A", "expr": '{service_name="Oficina.API"}'}], "options": {"showTime": True}})
dashboard = {
    "uid": "oficina-local", "title": "Oficina — demonstracao local", "schemaVersion": 39, "version": 1,
    "timezone": "utc", "refresh": "5s", "time": {"from": "now-30m", "to": "now"}, "panels": panels,
    "tags": ["oficina", "local", "tech-challenge"],
    "links": [{"title": "Traces: Explore → Oficina Traces", "url": "/explore", "type": "link"}],
}
if __name__ == "__main__":
    path = Path(__file__).parent / "grafana/dashboards/oficina.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(dashboard, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
