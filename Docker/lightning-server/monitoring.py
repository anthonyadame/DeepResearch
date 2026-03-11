# Monitoring Module for Lightning Server
# Prometheus metrics export and system health monitoring

import logging
import time
import psutil
from typing import Dict, Any, Optional
from datetime import datetime
from collections import defaultdict

logger = logging.getLogger(__name__)


class MetricsCollector:
    """
    Collects and exports metrics for Prometheus.
    """
    
    def __init__(self):
        """Initialize metrics collector."""
        self.request_count = defaultdict(int)
        self.request_duration = defaultdict(list)
        self.error_count = defaultdict(int)
        self.active_requests = 0
        self.start_time = time.time()
        
        # Feature-specific metrics
        self.apo_optimizations = 0
        self.verl_jobs = 0
        self.verl_jobs_completed = 0
        self.verl_jobs_failed = 0
        
        logger.info("✅ Metrics Collector initialized")
    
    def record_request(self, endpoint: str, duration: float, status_code: int):
        """
        Record HTTP request metrics.
        
        Args:
            endpoint: API endpoint path
            duration: Request duration in seconds
            status_code: HTTP status code
        """
        self.request_count[endpoint] += 1
        self.request_duration[endpoint].append(duration)
        
        if status_code >= 400:
            self.error_count[endpoint] += 1
    
    def increment_active_requests(self):
        """Increment active request counter."""
        self.active_requests += 1
    
    def decrement_active_requests(self):
        """Decrement active request counter."""
        self.active_requests = max(0, self.active_requests - 1)
    
    def record_apo_optimization(self):
        """Record APO optimization completed."""
        self.apo_optimizations += 1
    
    def record_verl_job(self, status: str):
        """
        Record VERL job status change.
        
        Args:
            status: Job status (started, completed, failed)
        """
        if status == "started":
            self.verl_jobs += 1
        elif status == "completed":
            self.verl_jobs_completed += 1
        elif status == "failed":
            self.verl_jobs_failed += 1
    
    def get_metrics(self) -> Dict[str, Any]:
        """
        Get all collected metrics.
        
        Returns:
            Dict with metrics in Prometheus-compatible format
        """
        uptime_seconds = time.time() - self.start_time
        
        # Calculate request statistics
        total_requests = sum(self.request_count.values())
        total_errors = sum(self.error_count.values())
        
        # Calculate average response times
        avg_response_times = {}
        for endpoint, durations in self.request_duration.items():
            if durations:
                avg_response_times[endpoint] = sum(durations) / len(durations)
        
        return {
            "uptime_seconds": uptime_seconds,
            "total_requests": total_requests,
            "total_errors": total_errors,
            "active_requests": self.active_requests,
            "error_rate": (total_errors / total_requests) if total_requests > 0 else 0,
            
            "requests_by_endpoint": dict(self.request_count),
            "errors_by_endpoint": dict(self.error_count),
            "avg_response_times": avg_response_times,
            
            "apo_optimizations_total": self.apo_optimizations,
            "verl_jobs_total": self.verl_jobs,
            "verl_jobs_completed": self.verl_jobs_completed,
            "verl_jobs_failed": self.verl_jobs_failed,
            "verl_jobs_success_rate": (
                (self.verl_jobs_completed / self.verl_jobs) if self.verl_jobs > 0 else 0
            )
        }
    
    def get_prometheus_metrics(self) -> str:
        """
        Get metrics in Prometheus text format.
        
        Returns:
            Prometheus-formatted metrics string
        """
        metrics = self.get_metrics()
        
        lines = [
            "# HELP lightning_uptime_seconds Server uptime in seconds",
            "# TYPE lightning_uptime_seconds gauge",
            f"lightning_uptime_seconds {metrics['uptime_seconds']:.2f}",
            "",
            "# HELP lightning_requests_total Total HTTP requests",
            "# TYPE lightning_requests_total counter",
            f"lightning_requests_total {metrics['total_requests']}",
            "",
            "# HELP lightning_errors_total Total HTTP errors",
            "# TYPE lightning_errors_total counter",
            f"lightning_errors_total {metrics['total_errors']}",
            "",
            "# HELP lightning_active_requests Currently active requests",
            "# TYPE lightning_active_requests gauge",
            f"lightning_active_requests {metrics['active_requests']}",
            "",
            "# HELP lightning_apo_optimizations_total Total APO optimizations",
            "# TYPE lightning_apo_optimizations_total counter",
            f"lightning_apo_optimizations_total {metrics['apo_optimizations_total']}",
            "",
            "# HELP lightning_verl_jobs_total Total VERL jobs started",
            "# TYPE lightning_verl_jobs_total counter",
            f"lightning_verl_jobs_total {metrics['verl_jobs_total']}",
            "",
            "# HELP lightning_verl_jobs_completed VERL jobs completed successfully",
            "# TYPE lightning_verl_jobs_completed counter",
            f"lightning_verl_jobs_completed {metrics['verl_jobs_completed']}",
            "",
            "# HELP lightning_verl_jobs_failed VERL jobs failed",
            "# TYPE lightning_verl_jobs_failed counter",
            f"lightning_verl_jobs_failed {metrics['verl_jobs_failed']}",
            "",
        ]
        
        # Add per-endpoint metrics
        for endpoint, count in metrics['requests_by_endpoint'].items():
            endpoint_safe = endpoint.replace("/", "_").replace("-", "_")
            lines.append(f"lightning_requests_by_endpoint{{endpoint=\"{endpoint}\"}} {count}")
        
        return "\n".join(lines)


class HealthMonitor:
    """
    System health monitoring.
    """
    
    def __init__(self):
        """Initialize health monitor."""
        logger.info("✅ Health Monitor initialized")
    
    def get_system_health(self) -> Dict[str, Any]:
        """
        Get comprehensive system health metrics.
        
        Returns:
            Dict with system health information
        """
        try:
            cpu_percent = psutil.cpu_percent(interval=0.1)
            memory = psutil.virtual_memory()
            disk = psutil.disk_usage('/')
            
            return {
                "status": "healthy",
                "timestamp": datetime.utcnow().isoformat(),
                "system": {
                    "cpu_percent": cpu_percent,
                    "memory": {
                        "total_mb": memory.total / (1024 * 1024),
                        "available_mb": memory.available / (1024 * 1024),
                        "used_percent": memory.percent
                    },
                    "disk": {
                        "total_gb": disk.total / (1024 ** 3),
                        "free_gb": disk.free / (1024 ** 3),
                        "used_percent": disk.percent
                    }
                },
                "checks": {
                    "cpu_ok": cpu_percent < 90,
                    "memory_ok": memory.percent < 90,
                    "disk_ok": disk.percent < 90
                }
            }
        
        except Exception as e:
            logger.error(f"❌ Failed to get system health: {e}", exc_info=True)
            return {
                "status": "error",
                "error": str(e)
            }
    
    def check_service_health(self, service_name: str, endpoint: str) -> Dict[str, Any]:
        """
        Check external service health.
        
        Args:
            service_name: Service identifier
            endpoint: Health check endpoint URL
        
        Returns:
            Dict with service health status
        """
        import requests
        
        try:
            start_time = time.time()
            response = requests.get(endpoint, timeout=5)
            response_time = (time.time() - start_time) * 1000  # ms
            
            is_healthy = response.status_code == 200
            
            return {
                "service": service_name,
                "healthy": is_healthy,
                "status_code": response.status_code,
                "response_time_ms": response_time
            }
        
        except Exception as e:
            return {
                "service": service_name,
                "healthy": False,
                "error": str(e)
            }


# Global instances
metrics_collector = MetricsCollector()
health_monitor = HealthMonitor()


def get_monitoring_dashboard() -> Dict[str, Any]:
    """
    Get comprehensive monitoring dashboard data.
    
    Returns:
        Dict with all monitoring information
    """
    return {
        "metrics": metrics_collector.get_metrics(),
        "system_health": health_monitor.get_system_health(),
        "timestamp": datetime.utcnow().isoformat()
    }


if __name__ == "__main__":
    # Example usage
    print("Monitoring Module")
    print("=" * 60)
    print()
    
    # Test metrics collection
    metrics_collector.record_request("/api/test", 0.15, 200)
    metrics_collector.record_request("/api/test", 0.12, 200)
    metrics_collector.record_request("/api/error", 0.25, 500)
    metrics_collector.record_apo_optimization()
    metrics_collector.record_verl_job("started")
    metrics_collector.record_verl_job("completed")
    
    print("Collected Metrics:")
    metrics = metrics_collector.get_metrics()
    for key, value in metrics.items():
        if isinstance(value, dict):
            print(f"  {key}:")
            for k, v in value.items():
                print(f"    {k}: {v}")
        else:
            print(f"  {key}: {value}")
    print()
    
    # Test Prometheus format
    print("Prometheus Metrics:")
    print(metrics_collector.get_prometheus_metrics())
    print()
    
    # Test health monitoring
    print("System Health:")
    health = health_monitor.get_system_health()
    print(f"  Status: {health['status']}")
    print(f"  CPU: {health['system']['cpu_percent']}%")
    print(f"  Memory: {health['system']['memory']['used_percent']}%")
    print(f"  Disk: {health['system']['disk']['used_percent']}%")
    print()
    
    print("✅ Monitoring module loaded successfully")
