"""
Lightning Server - Agent Orchestration with Microsoft Agent-Lightning
Provides FastAPI wrapper around Agent-Lightning's LightningStore with:
- Persistent MongoDB storage or in-memory fallback
- OpenTelemetry telemetry and metrics
- vLLM/LLMProxy integration
- VERL and APO algorithm support
"""
import os
import sys
import logging
import asyncio
from datetime import datetime
from typing import Dict, List, Optional, Any, Sequence
from enum import Enum
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse, FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field
import uvicorn

# OpenTelemetry imports (optional)
OTEL_AVAILABLE = False
try:
    from opentelemetry import trace
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor, ConsoleSpanExporter
    from opentelemetry.sdk.resources import Resource, SERVICE_NAME, SERVICE_VERSION
    from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
    from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
    from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
    OTEL_AVAILABLE = True
except ImportError as e:
    print(f"⚠️  OpenTelemetry not available: {e}")
    print("   Server will run without telemetry instrumentation")

# Import configuration
from config import config

# Import VERL manager
try:
    from verl_manager import VERLTrainingManager, VERL_AVAILABLE
except ImportError:
    VERL_AVAILABLE = False
    logger.warning("⚠️  VERL manager not available")

# Import APO manager
try:
    from apo_manager import APOManager
    APO_AVAILABLE = True
except ImportError as e:
    APO_AVAILABLE = False
    print(f"⚠️  APO manager not available: {e}")

# Import Production Hardening modules
try:
    from error_recovery import (
        get_circuit_breaker,
        get_retry_policy,
        get_all_circuit_breaker_status
    )
    ERROR_RECOVERY_AVAILABLE = True
    logger.info("✅ Error Recovery module loaded")
except ImportError as e:
    ERROR_RECOVERY_AVAILABLE = False
    print(f"⚠️  Error Recovery not available: {e}")

try:
    from security import (
        api_key_manager,
        rate_limiter,
        input_validator,
        get_security_status
    )
    SECURITY_AVAILABLE = True
    logger.info("✅ Security module loaded")
except ImportError as e:
    SECURITY_AVAILABLE = False
    print(f"⚠️  Security not available: {e}")

try:
    from monitoring import (
        metrics_collector,
        health_monitor,
        get_monitoring_dashboard
    )
    MONITORING_AVAILABLE = True
    logger.info("✅ Monitoring module loaded")
except ImportError as e:
    MONITORING_AVAILABLE = False
    print(f"⚠️  Monitoring not available: {e}")

# Import Agent Lightning components
try:
    from agentlightning import (
        InMemoryLightningStore,
        LightningStoreServer,
        Rollout,
        Attempt,
        Span,
        ResourcesUpdate,
    )
    from agentlightning.types import (
        TaskInput,
        RolloutConfig,
        RolloutStatus,
        AttemptStatus,
    )
    AGENT_LIGHTNING_AVAILABLE = True

    # Try importing MongoDB store
    try:
        from agentlightning.store.mongo import MongoLightningStore
        MONGODB_AVAILABLE = True
    except ImportError:
        MONGODB_AVAILABLE = False
        logging.warning("⚠️  MongoDB support not available. Install with: pip install agentlightning[mongo]")

except ImportError as e:
    AGENT_LIGHTNING_AVAILABLE = False
    MONGODB_AVAILABLE = False
    logging.warning(f"⚠️  Agent Lightning not available: {e}")
    logging.warning("   Install with: pip install agentlightning")


# Configure logging
logging.basicConfig(
    level=getattr(logging, config.server.log_level.upper()),
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# ========== Configure OpenTelemetry ==========
def configure_opentelemetry():
    """Configure OpenTelemetry tracing with OTLP export"""
    if not OTEL_AVAILABLE:
        logger.warning("⚠️  OpenTelemetry not available - skipping telemetry configuration")
        return False

    try:
        # Create resource with service information
        resource = Resource.create({
            SERVICE_NAME: "lightning-server",
            SERVICE_VERSION: "1.0.0",
            "deployment.environment": config.server.environment,
            "service.instance.id": config.mongodb.partition_id,
        })

        # Create tracer provider
        tracer_provider = TracerProvider(resource=resource)

        # Add OTLP exporter to send traces to Lightning Store
        if config.opentelemetry.otlp_endpoint:
            otlp_exporter = OTLPSpanExporter(
                endpoint=config.opentelemetry.otlp_endpoint,
                timeout=config.opentelemetry.export_timeout
            )
            tracer_provider.add_span_processor(
                BatchSpanProcessor(otlp_exporter)
            )
            logger.info(f"✅ OTLP exporter configured: {config.opentelemetry.otlp_endpoint}")

        # Add console exporter for development/debugging
        if config.opentelemetry.console_export:
            console_exporter = ConsoleSpanExporter()
            tracer_provider.add_span_processor(
                BatchSpanProcessor(console_exporter)
            )
            logger.info("✅ Console span exporter enabled (debug mode)")

        # Set global tracer provider
        trace.set_tracer_provider(tracer_provider)

        logger.info("✅ OpenTelemetry tracing configured successfully")
        return True

    except Exception as e:
        logger.error(f"❌ Failed to configure OpenTelemetry: {e}", exc_info=True)
        return False

# Initialize OpenTelemetry
OTEL_CONFIGURED = configure_opentelemetry()
tracer = trace.get_tracer(__name__) if OTEL_AVAILABLE else None

# Log startup configuration
logger.info("=" * 60)
logger.info("Lightning Server Starting")
logger.info("=" * 60)
logger.info(f"Configuration: {config.get_info()}")
logger.info(f"OpenTelemetry: {'✅ Enabled' if OTEL_CONFIGURED else '❌ Disabled'}")

# Legacy configuration (for backward compatibility)
RMPT_ENABLED = config.server.rmpt_enabled
RLCS_ENABLED = config.server.rlcs_enabled
RMPT_STRATEGY = config.server.rmpt_strategy
RMPT_MAX_TASKS = config.server.rmpt_max_tasks
RLCS_CONFIDENCE_THRESHOLD = config.server.rlcs_confidence_threshold
LIGHTNING_PORT = config.server.lightning_port

app = FastAPI(
    title="Lightning Server",
    description="Agent orchestration with Microsoft Agent-Lightning",
    version="1.0.0"
)

# Instrument FastAPI with OpenTelemetry (must be done after app creation)
if OTEL_CONFIGURED and OTEL_AVAILABLE:
    try:
        FastAPIInstrumentor.instrument_app(app)
        logger.info("✅ FastAPI instrumented with OpenTelemetry")
    except Exception as e:
        logger.error(f"❌ Failed to instrument FastAPI: {e}")

# Instrument httpx client for outbound HTTP tracing
if OTEL_CONFIGURED and OTEL_AVAILABLE:
    try:
        HTTPXClientInstrumentor().instrument()
        logger.info("✅ httpx client instrumented with OpenTelemetry")
    except Exception as e:
        logger.error(f"❌ Failed to instrument httpx: {e}")

# ========== Mount Dashboard (if available) ==========
dashboard_path = Path("/app/agentlightning/dashboard")
if dashboard_path.exists() and dashboard_path.is_dir():
    app.mount("/dashboard", StaticFiles(directory=str(dashboard_path), html=True), name="dashboard")
    assets_path = dashboard_path / "assets"
    if assets_path.exists():
        # Vite build emits assets at root (/assets/*); mount them explicitly
        app.mount("/assets", StaticFiles(directory=str(assets_path), html=False), name="dashboard-assets")
    logger.info(f"✅ Agent Lightning Dashboard mounted at /dashboard")
else:
    logger.warning(f"⚠️ Dashboard not found at {dashboard_path}")

# ========== Initialize Agent Lightning Store ==========
lightning_store = None
store_server = None
verl_manager = None
apo_manager = None
LIGHTNING_STORE_INITIALIZED = False
STORE_TYPE = "none"
VERL_INITIALIZED = False
APO_INITIALIZED = False


async def initialize_lightning_store():
    """Initialize Lightning Store (MongoDB or in-memory fallback)"""
    global lightning_store, LIGHTNING_STORE_INITIALIZED, STORE_TYPE

    if not AGENT_LIGHTNING_AVAILABLE:
        logger.error("❌ Agent Lightning not available - cannot initialize store")
        return False

    try:
        # Validate configuration
        config.validate()

        # Try MongoDB first if configured
        if config.use_mongodb and MONGODB_AVAILABLE:
            logger.info("🔄 Initializing MongoLightningStore...")
            logger.info(f"   MongoDB URI: {config.mongodb.uri}")
            logger.info(f"   Database: {config.mongodb.database}")
            logger.info(f"   Partition: {config.mongodb.partition_id}")

            try:
                # Create metrics tracker if enabled
                tracker = None
                if config.prometheus_enabled:
                    try:
                        from agentlightning.tracker import PrometheusMetricsBackend
                        import prometheus_client
                        tracker = PrometheusMetricsBackend(
                            port=config.server.prometheus_port,
                            registry=prometheus_client.REGISTRY
                        )
                        logger.info(f"✅ Prometheus metrics enabled on port {config.server.prometheus_port}")
                    except Exception as e:
                        logger.warning(f"⚠️  Failed to initialize Prometheus metrics: {e}")

                lightning_store = MongoLightningStore(
                    mongo_uri=config.mongodb.uri,
                    database_name=config.mongodb.database,
                    partition_id=config.mongodb.partition_id,
                    mongo_client_kwargs=config.mongodb.get_client_kwargs(),
                    tracker=tracker,
                    scan_debounce_seconds=10.0
                )

                # Test MongoDB connection
                logger.info("🔄 Testing MongoDB connection...")
                stats = await lightning_store.statistics()
                logger.info(f"✅ MongoDB connection successful!")
                logger.info(f"   Store statistics: {stats}")

                LIGHTNING_STORE_INITIALIZED = True
                STORE_TYPE = "mongodb"
                logger.info("✅ MongoLightningStore initialized successfully")
                return True

            except Exception as e:
                logger.error(f"❌ Failed to initialize MongoLightningStore: {e}", exc_info=True)
                logger.warning("⚠️  Falling back to InMemoryLightningStore...")

        # Fallback to in-memory store
        if not LIGHTNING_STORE_INITIALIZED:
            logger.info("🔄 Initializing InMemoryLightningStore...")

            # Create metrics tracker if enabled
            tracker = None
            if config.prometheus_enabled:
                try:
                    from agentlightning.tracker import PrometheusMetricsBackend
                    import prometheus_client
                    tracker = PrometheusMetricsBackend(
                        port=config.server.prometheus_port,
                        registry=prometheus_client.REGISTRY
                    )
                    logger.info(f"✅ Prometheus metrics enabled on port {config.server.prometheus_port}")
                except Exception as e:
                    logger.warning(f"⚠️  Failed to initialize Prometheus metrics: {e}")

            lightning_store = InMemoryLightningStore(
                thread_safe=True,
                tracker=tracker
            )
            LIGHTNING_STORE_INITIALIZED = True
            STORE_TYPE = "in-memory"
            logger.info("✅ InMemoryLightningStore initialized successfully")
            logger.warning("⚠️  Using in-memory storage - data will be lost on restart!")
            return True

    except Exception as e:
        logger.error(f"❌ Failed to initialize Lightning Store: {e}", exc_info=True)
        LIGHTNING_STORE_INITIALIZED = False
        return False


async def check_mongodb_health() -> Dict[str, Any]:
    """Check MongoDB connection health"""
    if STORE_TYPE != "mongodb":
        return {"available": False, "reason": "Not using MongoDB"}

    try:
        # Try to get statistics (will fail if MongoDB is down)
        stats = await lightning_store.statistics()
        return {
            "available": True,
            "healthy": True,
            "statistics": stats
        }
    except Exception as e:
        return {
            "available": True,
            "healthy": False,
            "error": str(e)
        }


# Store initialization will happen on startup
@app.on_event("startup")
async def startup_event():
    """Initialize store on startup"""
    global store_server, verl_manager, apo_manager, VERL_INITIALIZED, APO_INITIALIZED

    logger.info("🚀 Starting Lightning Server...")

    # Initialize store
    success = await initialize_lightning_store()
    if not success:
        logger.error("❌ Failed to initialize Lightning Store")
        logger.error("   Server will continue but Lightning features will be unavailable")

    # Initialize store server if store is available
    if lightning_store:
        try:
            logger.info("🔄 Initializing LightningStoreServer...")
            store_server = LightningStoreServer(
                store=lightning_store,
                host="0.0.0.0",
                port=LIGHTNING_PORT,
                cors_allow_origins=config.server.cors_allow_origins.split(",")
            )
            app.include_router(store_server.router, prefix="/api")
            logger.info("✅ Lightning Store API mounted at /api")
            logger.info(f"✅ OTLP traces endpoint: {store_server.otlp_traces_endpoint()}")
        except Exception as e:
            logger.error(f"❌ Failed to initialize LightningStoreServer: {e}", exc_info=True)

    # Initialize VERL manager if enabled
    if config.verl.enabled and VERL_AVAILABLE:
        try:
            logger.info("🔄 Initializing VERL Training Manager...")
            verl_manager = VERLTrainingManager(
                config=config.verl,
                lightning_store=lightning_store,
                enable_ray=True
            )
            VERL_INITIALIZED = True
            logger.info("✅ VERL Training Manager initialized")
        except Exception as e:
            logger.error(f"❌ Failed to initialize VERL manager: {e}", exc_info=True)
            logger.error("   VERL training features will be unavailable")

    # Initialize APO manager if enabled
    if config.apo.enabled and APO_AVAILABLE:
        try:
            logger.info("🔄 Initializing APO Optimization Manager...")
            apo_manager = APOManager(
                config=config.apo,
                llm_client=None,  # Will be created by APO manager
                lightning_store=lightning_store
            )
            APO_INITIALIZED = True
            logger.info("✅ APO Optimization Manager initialized")
        except Exception as e:
            logger.error(f"❌ Failed to initialize APO manager: {e}", exc_info=True)
            logger.error("   APO optimization features will be unavailable")

    logger.info("=" * 60)
    logger.info("✅ Lightning Server Started Successfully")
    logger.info(f"   Store Type: {STORE_TYPE}")
    logger.info(f"   Port: {LIGHTNING_PORT}")
    logger.info(f"   API Docs: http://localhost:{LIGHTNING_PORT}/docs")
    if dashboard_path.exists():
        logger.info(f"   Dashboard: http://localhost:{LIGHTNING_PORT}/dashboard")
    if VERL_INITIALIZED:
        logger.info(f"   VERL Training: ✅ Enabled")
    if APO_INITIALIZED:
        logger.info(f"   APO Optimization: ✅ Enabled")
    logger.info("=" * 60)


@app.on_event("shutdown")
async def shutdown_event():
    """Cleanup on shutdown"""
    logger.info("🛑 Shutting down Lightning Server...")

    # Cleanup VERL manager if initialized
    if VERL_INITIALIZED and verl_manager:
        try:
            logger.info("🔄 Cleaning up VERL Training Manager...")
            await verl_manager.cleanup()
            logger.info("✅ VERL cleanup complete")
        except Exception as e:
            logger.error(f"❌ Error cleaning up VERL manager: {e}")

    # Cleanup APO manager if initialized
    if APO_INITIALIZED and apo_manager:
        try:
            logger.info("🔄 Cleaning up APO Optimization Manager...")
            # APO manager doesn't need explicit cleanup (stateless)
            logger.info("✅ APO cleanup complete")
        except Exception as e:
            logger.error(f"❌ Error cleaning up APO manager: {e}")

    # Close MongoDB connection if using MongoDB
    if STORE_TYPE == "mongodb" and lightning_store:
        try:
            logger.info("🔄 Closing MongoDB connection...")
            await lightning_store.close()
            logger.info("✅ MongoDB connection closed")
        except Exception as e:
            logger.error(f"❌ Error closing MongoDB connection: {e}")

    logger.info("✅ Lightning Server shut down successfully")

# ========== C# Compatibility Models =========
class TaskStatus(str, Enum):
    """Task status compatible with C# TaskStatus enum"""
    SUBMITTED = "Submitted"
    PENDING = "Pending"
    IN_PROGRESS = "InProgress"
    COMPLETED = "Completed"
    FAILED = "Failed"
    VERIFICATION_REQUIRED = "VerificationRequired"
    VERIFICATION_PASSED = "VerificationPassed"
    VERIFICATION_FAILED = "VerificationFailed"

class AgentRegistration(BaseModel):
    agentId: str
    agentType: str
    clientId: str
    capabilities: Dict[str, Any] = Field(default_factory=dict)
    registeredAt: datetime = Field(default_factory=datetime.utcnow)
    isActive: bool = True

class AgentTask(BaseModel):
    id: str
    name: str
    description: str
    input: Dict[str, Any] = Field(default_factory=dict)
    status: TaskStatus = TaskStatus.SUBMITTED
    priority: int = 0
    submittedAt: datetime = Field(default_factory=datetime.utcnow)
    resultData: Optional[str] = None
    verificationRequired: bool = True

class AgentTaskResult(BaseModel):
    taskId: str
    status: TaskStatus
    result: Optional[str] = None
    completedAt: Optional[datetime] = None

class VerificationResult(BaseModel):
    taskId: str
    isValid: bool
    confidence: float
    issues: List[str] = Field(default_factory=list)
    verifiedAt: datetime = Field(default_factory=datetime.utcnow)

class ReasoningStep(BaseModel):
    stepNumber: int
    description: str
    logic: str
    conclusions: List[str] = Field(default_factory=list)
    confidence: float

class ReasoningChainValidation(BaseModel):
    isValid: bool
    score: float
    errors: List[str] = Field(default_factory=list)
    warnings: List[str] = Field(default_factory=list)
    validatedAt: datetime = Field(default_factory=datetime.utcnow)

class ConfidenceScore(BaseModel):
    score: float
    factors: Dict[str, float] = Field(default_factory=dict)
    reasoning: str = ""

class FactCheckResult(BaseModel):
    verifiedCount: int
    totalCount: int
    unreliableFacts: List[str] = Field(default_factory=list)
    verificationScore: float

class ConsistencyCheckResult(BaseModel):
    isConsistent: bool
    score: float
    contradictions: List[str] = Field(default_factory=list)

class LightningServerInfo(BaseModel):
    version: str = "1.0.0"
    rmptEnabled: bool = RMPT_ENABLED
    rlcsEnabled: bool = RLCS_ENABLED
    registeredAgents: int = 0
    activeConnections: int = 0
    startedAt: datetime = Field(default_factory=datetime.utcnow)
    agentLightningVersion: Optional[str] = None
    dashboardAvailable: bool = False
    dashboardUrl: Optional[str] = None

# ========== In-Memory Storage =========
registered_agents: Dict[str, AgentRegistration] = {}
server_start_time = datetime.utcnow()

# ========== Root & Dashboard Endpoints =========
@app.get("/")
async def root():
    """Root endpoint with links to dashboard and API"""
    dashboard_available = dashboard_path.exists()
    
    return {
        "service": "Lightning Server",
        "version": "1.0.0",
        "status": "running",
        "dashboardAvailable": dashboard_available,
        "dashboardUrl": "/dashboard" if dashboard_available else None,
        "apiDocs": "/docs",
        "health": "/health"
    }

# ========== Health & Info Endpoints =========
@app.get("/health")
async def health_check():
    """Comprehensive health check endpoint"""
    mongodb_health = await check_mongodb_health()

    health_status = {
        "status": "healthy" if LIGHTNING_STORE_INITIALIZED else "degraded",
        "timestamp": datetime.utcnow().isoformat(),
        "agentLightningAvailable": AGENT_LIGHTNING_AVAILABLE,
        "opentelemetry": {
            "enabled": OTEL_CONFIGURED,
            "otlpEndpoint": config.opentelemetry.otlp_endpoint if OTEL_CONFIGURED else None
        },
        "storage": {
            "type": STORE_TYPE,
            "initialized": LIGHTNING_STORE_INITIALIZED,
            "mongodb": mongodb_health if STORE_TYPE == "mongodb" else None
        },
        "dashboard": {
            "available": dashboard_path.exists(),
            "path": str(dashboard_path) if dashboard_path.exists() else None
        }
    }

    # Add store statistics if available
    if lightning_store:
        try:
            stats = await lightning_store.statistics()
            health_status["storage"]["statistics"] = stats
        except Exception as e:
            health_status["storage"]["statistics_error"] = str(e)

    # Return 503 if store is not initialized
    status_code = 200 if LIGHTNING_STORE_INITIALIZED else 503
    return JSONResponse(content=health_status, status_code=status_code)

@app.get("/api/health")
async def api_health_check():
    """Alternative health check endpoint"""
    return await health_check()

@app.get("/api/server/info", response_model=LightningServerInfo)
async def get_server_info():
    """Get Lightning Server information"""
    import agentlightning
    
    stats = None
    if lightning_store:
        try:
            stats = await lightning_store.statistics()
        except Exception as e:
            logger.error(f"Failed to get store statistics: {e}")
    
    dashboard_available = dashboard_path.exists()
    
    return LightningServerInfo(
        version="1.0.0",
        rmptEnabled=RMPT_ENABLED,
        rlcsEnabled=RLCS_ENABLED,
        registeredAgents=len(registered_agents),
        activeConnections=sum(1 for a in registered_agents.values() if a.isActive),
        startedAt=server_start_time,
        agentLightningVersion=agentlightning.__version__ if AGENT_LIGHTNING_AVAILABLE else None,
        dashboardAvailable=dashboard_available,
        dashboardUrl="/dashboard" if dashboard_available else None
    )

# ========== VERL Training Endpoints ==========
# Note: VERL manager is initialized in the startup_event() handler above

class VERLTrainingRequest(BaseModel):
    """VERL training job request"""
    project_name: Optional[str] = "verl_training"
    train_dataset: str
    val_dataset: Optional[str] = None
    model_path: str = "Qwen/Qwen2.5-0.5B-Instruct"
    learning_rate: float = 1e-5
    batch_size: int = 4
    n_rollouts: int = 16
    num_epochs: int = 1
    num_steps: Optional[int] = 1000
    reward_model_path: Optional[str] = None
    custom_reward_path: Optional[str] = None
    wandb_project: Optional[str] = None
    gradient_checkpointing: bool = False
    lora_rank: int = 0

@app.post("/verl/train")
async def start_verl_training(request: VERLTrainingRequest):
    """
    Start a new VERL training job.

    Returns job_id and initial status.
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled or failed to initialize"},
            status_code=503
        )

    try:
        # Create training job
        job_id = await verl_manager.create_training_job(request.dict())

        # Start training subprocess
        result = await verl_manager.start_training_subprocess(job_id)

        if result["success"]:
            return {
                "success": True,
                "job_id": job_id,
                "status": "running",
                "process_id": result["process_id"],
                "log_file": result["log_file"]
            }
        else:
            return JSONResponse(
                content={
                    "success": False,
                    "job_id": job_id,
                    "error": result.get("error", "Unknown error")
                },
                status_code=500
            )

    except Exception as e:
        logger.error(f"❌ Failed to start training: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/verl/jobs")
async def list_verl_jobs(limit: int = 10, status: Optional[str] = None):
    """
    List VERL training jobs with optional status filter.

    Args:
        limit: Maximum number of jobs to return (default: 10)
        status: Filter by status (pending, running, completed, failed, stopped)
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = await verl_manager.list_jobs(limit=limit, status=status)
        return result
    except Exception as e:
        logger.error(f"❌ Failed to list jobs: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/verl/jobs/{job_id}")
async def get_verl_job_status(job_id: str):
    """
    Get status and metrics for a specific VERL training job.

    Returns job metadata, current status, and latest metrics.
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = await verl_manager.get_job_status(job_id)

        if result["success"]:
            return result["job"]
        else:
            return JSONResponse(
                content={"error": result.get("error", "Job not found")},
                status_code=404
            )
    except Exception as e:
        logger.error(f"❌ Failed to get job status: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.delete("/verl/jobs/{job_id}")
async def stop_verl_job(job_id: str):
    """
    Stop a running VERL training job.

    Sends SIGTERM to the training process and updates job status.
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = await verl_manager.stop_training_job(job_id)

        if result["success"]:
            return result
        else:
            return JSONResponse(
                content={"error": result.get("error", "Failed to stop job")},
                status_code=400
            )
    except Exception as e:
        logger.error(f"❌ Failed to stop job: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


# ═══════════════════════════════════════════════════════════════════════════════
# VERL ADVANCED FEATURES API - Model Architectures, Distributed Training, Fine-Tuning
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/verl/configure/architecture")
async def configure_verl_architecture(
    architecture_type: str = "medium",
    model_class: str = "policy",
    custom_config: Optional[Dict[str, Any]] = None
):
    """
    Configure VERL model architecture.

    Args:
        architecture_type: Size preset ('small', 'medium', 'large')
        model_class: Model type ('policy', 'value', 'reward')
        custom_config: Optional custom configuration

    Returns:
        Model configuration with parameter counts and memory requirements
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = verl_manager.configure_model_architecture(
            architecture_type=architecture_type,
            model_class=model_class,
            custom_config=custom_config
        )

        if result["success"]:
            return result
        else:
            return JSONResponse(
                content={"error": result.get("error", "Configuration failed")},
                status_code=400
            )
    except Exception as e:
        logger.error(f"❌ Failed to configure architecture: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.post("/verl/configure/distributed")
async def configure_verl_distributed(
    num_gpus: int = 1,
    enable_gradient_accumulation: bool = False,
    accumulation_steps: int = 4,
    enable_mixed_precision: bool = False,
    precision_mode: str = "fp16",
    custom_config: Optional[Dict[str, Any]] = None
):
    """
    Configure VERL distributed training settings.

    Args:
        num_gpus: Number of GPUs to use (1-8)
        enable_gradient_accumulation: Enable gradient accumulation
        accumulation_steps: Number of accumulation steps
        enable_mixed_precision: Enable mixed precision training
        precision_mode: Precision mode ('fp16', 'bf16', 'mixed')
        custom_config: Optional custom configuration

    Returns:
        Distributed training configuration with effective batch size
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = verl_manager.configure_distributed_training(
            num_gpus=num_gpus,
            enable_gradient_accumulation=enable_gradient_accumulation,
            accumulation_steps=accumulation_steps,
            enable_mixed_precision=enable_mixed_precision,
            precision_mode=precision_mode,
            custom_config=custom_config
        )

        if result["success"]:
            return result
        else:
            return JSONResponse(
                content={"error": result.get("error", "Configuration failed")},
                status_code=400
            )
    except Exception as e:
        logger.error(f"❌ Failed to configure distributed training: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.post("/verl/configure/finetuning")
async def configure_verl_finetuning(
    enable_lora: bool = True,
    lora_rank: int = 8,
    lora_preset: str = "medium",
    enable_adapters: bool = False,
    adapter_size: int = 128,
    adapter_preset: str = "medium",
    model_info: Optional[Dict[str, Any]] = None
):
    """
    Configure parameter-efficient fine-tuning with LoRA and/or Adapters.

    Args:
        enable_lora: Enable LoRA fine-tuning
        lora_rank: LoRA rank (r)
        lora_preset: Preset config ('small', 'medium', 'large')
        enable_adapters: Enable adapter layers
        adapter_size: Adapter bottleneck dimension
        adapter_preset: Preset config ('small', 'medium', 'large')
        model_info: Optional model information dict

    Returns:
        Fine-tuning configuration with parameter efficiency statistics
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        result = verl_manager.configure_finetuning(
            enable_lora=enable_lora,
            lora_rank=lora_rank,
            lora_preset=lora_preset,
            enable_adapters=enable_adapters,
            adapter_size=adapter_size,
            adapter_preset=adapter_preset,
            model_info=model_info
        )

        if result["success"]:
            return result
        else:
            return JSONResponse(
                content={"error": result.get("error", "Configuration failed")},
                status_code=400
            )
    except Exception as e:
        logger.error(f"❌ Failed to configure fine-tuning: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.get("/verl/features")
async def get_verl_features():
    """
    Get status of all VERL advanced features.

    Returns:
        Feature availability and capabilities for model architectures,
        distributed training, and parameter-efficient fine-tuning.
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        return verl_manager.get_advanced_features_status()
    except Exception as e:
        logger.error(f"❌ Failed to get features status: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


# ═══════════════════════════════════════════════════════════════════════════════
# PRODUCTION HARDENING API - Monitoring, Security, Error Recovery
# ═══════════════════════════════════════════════════════════════════════════════

@app.get("/metrics")
async def get_metrics():
    """
    Get Prometheus-compatible metrics.

    Returns metrics in Prometheus text format for scraping.
    """
    if not MONITORING_AVAILABLE:
        return JSONResponse(
            content={"error": "Monitoring module not available"},
            status_code=503
        )

    try:
        metrics_text = metrics_collector.get_prometheus_metrics()
        return JSONResponse(
            content=metrics_text,
            media_type="text/plain"
        )
    except Exception as e:
        logger.error(f"❌ Failed to get metrics: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.get("/monitoring/dashboard")
async def get_monitoring_dashboard_endpoint():
    """
    Get comprehensive monitoring dashboard data.

    Returns:
        Complete monitoring information including metrics and system health
    """
    if not MONITORING_AVAILABLE:
        return JSONResponse(
            content={"error": "Monitoring module not available"},
            status_code=503
        )

    try:
        return get_monitoring_dashboard()
    except Exception as e:
        logger.error(f"❌ Failed to get monitoring dashboard: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.get("/security/status")
async def get_security_status_endpoint():
    """
    Get security module status.

    Returns:
        Security features status including API keys, rate limiting, and validation
    """
    if not SECURITY_AVAILABLE:
        return JSONResponse(
            content={"error": "Security module not available"},
            status_code=503
        )

    try:
        return get_security_status()
    except Exception as e:
        logger.error(f"❌ Failed to get security status: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.get("/error-recovery/circuit-breakers")
async def get_circuit_breakers_status():
    """
    Get status of all circuit breakers.

    Returns:
        Circuit breaker states for all protected services
    """
    if not ERROR_RECOVERY_AVAILABLE:
        return JSONResponse(
            content={"error": "Error Recovery module not available"},
            status_code=503
        )

    try:
        return get_all_circuit_breaker_status()
    except Exception as e:
        logger.error(f"❌ Failed to get circuit breaker status: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


@app.get("/system/health")
async def get_system_health():
    """
    Get comprehensive system health metrics.

    Returns:
        System health including CPU, memory, disk, and service checks
    """
    if not MONITORING_AVAILABLE:
        return JSONResponse(
            content={"error": "Monitoring module not available"},
            status_code=503
        )

    try:
        return health_monitor.get_system_health()
    except Exception as e:
        logger.error(f"❌ Failed to get system health: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )


async def stream_verl_logs(job_id: str, lines: int = 100):
    """
    Get recent log lines from a VERL training job.

    Args:
        job_id: Job identifier
        lines: Number of recent lines to return (default: 100)
    """
    if not verl_manager:
        return JSONResponse(
            content={"error": "VERL not enabled"},
            status_code=503
        )

    try:
        # Get job info to find log file
        result = await verl_manager.get_job_status(job_id)

        if not result["success"]:
            return JSONResponse(
                content={"error": "Job not found"},
                status_code=404
            )

        log_file = Path(result["job"].get("log_file", ""))

        if not log_file.exists():
            return {
                "job_id": job_id,
                "logs": [],
                "message": "Log file not yet created"
            }

        # Read last N lines
        with open(log_file, 'r') as f:
            all_lines = f.readlines()
            recent_lines = all_lines[-lines:] if len(all_lines) > lines else all_lines

        return {
            "job_id": job_id,
            "log_file": str(log_file),
            "total_lines": len(all_lines),
            "returned_lines": len(recent_lines),
            "logs": [line.rstrip() for line in recent_lines]
        }

    except Exception as e:
        logger.error(f"❌ Failed to get logs: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

# ═══════════════════════════════════════════════════════════════════════════════
# APO (Agent Prompt Optimization) Endpoints
# ═══════════════════════════════════════════════════════════════════════════════

class APOOptimizationRequest(BaseModel):
    """Request model for APO prompt optimization"""
    prompt_name: str = Field(..., description="Unique name for this prompt")
    initial_prompt: str = Field(..., description="Initial prompt template to optimize")
    domain: str = Field(default="general", description="Domain/category of the prompt")
    description: Optional[str] = Field(None, description="Description of prompt purpose")
    iterations: int = Field(default=5, description="Number of optimization iterations")
    evaluation_samples: int = Field(default=10, description="Number of evaluation samples")
    model: str = Field(default="Qwen/Qwen3.5-2B-Instruct", description="Model to use")
    optimization_strategy: str = Field(default="iterative_refinement", description="Strategy")
    evaluation_criteria: Optional[List[str]] = Field(None, description="Evaluation criteria")

@app.post("/apo/optimize")
async def optimize_prompt(request: APOOptimizationRequest):
    """
    Start a prompt optimization run.

    This endpoint initiates an asynchronous optimization process that:
    1. Creates a new optimization run
    2. Iteratively refines the prompt
    3. Evaluates each version
    4. Stores results in MongoDB

    Returns immediately with a run_id for tracking progress.
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        result = await apo_manager.optimize_prompt(
            prompt_name=request.prompt_name,
            initial_prompt=request.initial_prompt,
            domain=request.domain,
            description=request.description,
            iterations=request.iterations,
            evaluation_samples=request.evaluation_samples,
            model=request.model,
            optimization_strategy=request.optimization_strategy,
            evaluation_criteria=request.evaluation_criteria
        )

        if result.get("success"):
            return {
                "message": "Optimization started",
                "run_id": result["run_id"],
                "prompt_id": result["prompt_id"],
                "iterations": request.iterations,
                "model": request.model,
                "status_endpoint": f"/apo/runs/{result['run_id']}"
            }
        else:
            return JSONResponse(
                content={"error": result.get("error", "Unknown error")},
                status_code=400
            )

    except Exception as e:
        logger.error(f"❌ Failed to start optimization: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/apo/runs")
async def list_optimization_runs(
    limit: int = 10,
    status: Optional[str] = None
):
    """
    List recent optimization runs.

    Args:
        limit: Maximum number of runs to return (default: 10)
        status: Filter by status (pending/running/completed/failed)
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        result = await apo_manager.list_runs(limit=limit, status=status)

        if result.get("success"):
            return {
                "runs": result["runs"],
                "count": result["count"],
                "limit": limit,
                "filter": {"status": status} if status else {}
            }
        else:
            return JSONResponse(
                content={"error": result.get("error", "Unknown error")},
                status_code=400
            )

    except Exception as e:
        logger.error(f"❌ Failed to list runs: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/apo/runs/{run_id}")
async def get_optimization_run(run_id: str):
    """
    Get detailed status of an optimization run.

    Returns:
        - Run metadata
        - Iteration results
        - Best version information
        - Performance metrics
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        result = await apo_manager.get_run_status(run_id)

        if result.get("success"):
            return result["run"]
        else:
            return JSONResponse(
                content={"error": result.get("error", "Run not found")},
                status_code=404
            )

    except Exception as e:
        logger.error(f"❌ Failed to get run status: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

# Strategy Comparison Endpoint
class StrategyComparisonRequest(BaseModel):
    """Request to compare optimization strategies."""
    strategies: List[str] = Field(description="Strategies to compare (e.g., ['iterative_refinement', 'beam_search', 'genetic_algorithm'])")
    initial_prompt: str = Field(description="Starting prompt for all strategies")
    iterations: int = Field(default=5, description="Iterations per strategy")
    domain: str = Field(default="general", description="Optimization domain")
    criteria: Optional[List[str]] = Field(None, description="Evaluation criteria")
    model: str = Field(default="gpt-4", description="Model to use")
    priority: str = Field(default="balanced", description="Priority: 'speed', 'quality', 'balanced', or 'robustness'")
    max_duration_seconds: Optional[float] = Field(None, description="Maximum duration constraint")
    min_quality_score: Optional[float] = Field(None, description="Minimum quality constraint")

@app.post("/apo/compare-strategies")
async def compare_strategies(request: StrategyComparisonRequest):
    """
    Compare multiple optimization strategies side-by-side.

    This endpoint runs multiple strategies in parallel and provides:
    - Performance metrics comparison
    - Cost/quality trade-off analysis
    - Strategy recommendation based on requirements

    Available strategies:
    - iterative_refinement: Fast, single-path optimization
    - beam_search: Quality-focused, multi-path exploration
    - genetic_algorithm: Robust, population-based evolution

    Priority modes:
    - speed: Recommend fastest strategy
    - quality: Recommend highest-scoring strategy
    - balanced: Balance quality/speed/efficiency
    - robustness: Prefer diverse, population-based approaches
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        result = await apo_manager.compare_strategies(
            strategies=request.strategies,
            initial_prompt=request.initial_prompt,
            iterations=request.iterations,
            domain=request.domain,
            criteria=request.criteria,
            model=request.model,
            priority=request.priority,
            max_duration_seconds=request.max_duration_seconds,
            min_quality_score=request.min_quality_score
        )

        if result.get("success"):
            return result
        else:
            return JSONResponse(
                content={"error": result.get("error", "Comparison failed")},
                status_code=400
            )

    except Exception as e:
        logger.error(f"❌ Strategy comparison failed: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/apo/prompts")
async def list_prompts(
    limit: int = 10,
    domain: Optional[str] = None
):
    """
    List optimized prompts.

    Args:
        limit: Maximum number of prompts to return (default: 10)
        domain: Filter by domain
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        result = await apo_manager.list_prompts(limit=limit, domain=domain)

        if result.get("success"):
            return {
                "prompts": result["prompts"],
                "count": result["count"],
                "limit": limit,
                "filter": {"domain": domain} if domain else {}
            }
        else:
            return JSONResponse(
                content={"error": result.get("error", "Unknown error")},
                status_code=400
            )

    except Exception as e:
        logger.error(f"❌ Failed to list prompts: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

@app.get("/apo/prompts/{prompt_id}")
async def get_prompt_details(prompt_id: str):
    """
    Get detailed information about a specific prompt.

    Returns:
        - Prompt metadata
        - All versions with scores
        - Optimization history
    """
    if not apo_manager:
        return JSONResponse(
            content={"error": "APO not enabled"},
            status_code=503
        )

    try:
        if apo_manager.mongo_db is None:
            return JSONResponse(
                content={"error": "MongoDB not available"},
                status_code=503
            )

        collection = apo_manager.mongo_db[apo_manager.prompts_collection_name]
        prompt = await collection.find_one({"_id": prompt_id})

        if not prompt:
            return JSONResponse(
                content={"error": "Prompt not found"},
                status_code=404
            )

        return prompt

    except Exception as e:
        logger.error(f"❌ Failed to get prompt: {e}", exc_info=True)
        return JSONResponse(
            content={"error": str(e)},
            status_code=500
        )

# ========== Agent Management =========
@app.post("/api/agents/register", response_model=AgentRegistration)
async def register_agent(registration: AgentRegistration):
    """Register a new agent"""
    logger.info(f"Registering agent: {registration.agentId} ({registration.agentType})")
    
    registration.registeredAt = datetime.utcnow()
    registration.isActive = True
    registered_agents[registration.agentId] = registration
    
    if lightning_store:
        try:
            resources = {f"agent:{registration.agentId}": registration.model_dump_json()}
            await lightning_store.add_resources(resources)
        except Exception as e:
            logger.error(f"Failed to store agent in Lightning Store: {e}")
    
    return registration

@app.get("/api/agents/{agent_id}")
async def get_agent(agent_id: str):
    """Get agent by ID"""
    if agent_id not in registered_agents:
        raise HTTPException(status_code=404, detail=f"Agent {agent_id} not found")
    return registered_agents[agent_id]

@app.get("/api/agents")
async def list_agents():
    """List all registered agents"""
    return list(registered_agents.values())

# ========== Task Management (Lightning Store integration) =========
@app.post("/api/tasks/submit", response_model=AgentTaskResult)
async def submit_task(payload: Dict[str, Any]):
    """Submit a task using Lightning Store"""
    if not lightning_store:
        raise HTTPException(status_code=503, detail="Lightning Store not available")
    
    agent_id = payload.get("agentId")
    task_data = payload.get("task")
    
    if not agent_id or not task_data:
        raise HTTPException(status_code=400, detail="Missing agentId or task data")
    
    try:
        task_input = {
            "agentId": agent_id,
            "name": task_data.get("name"),
            "description": task_data.get("description"),
            "input": task_data.get("input", {}),
            "priority": task_data.get("priority", 0),
        }
        
        rollout = await lightning_store.enqueue_rollout(
            input=task_input,
            mode="train" if task_data.get("verificationRequired", True) else "val",
            metadata={
                "agentId": agent_id,
                "originalTaskId": task_data.get("id"),
                "priority": task_data.get("priority", 0)
            }
        )
        
        logger.info(f"Task submitted: {rollout.rollout_id}")
        
        return AgentTaskResult(
            taskId=rollout.rollout_id,
            status=TaskStatus.SUBMITTED,
            result=None,
            completedAt=None
        )
    
    except Exception as e:
        logger.error(f"Failed to submit task: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/api/agents/{agent_id}/tasks/pending")
async def get_pending_tasks(agent_id: str):
    """Get pending tasks for an agent"""
    if not lightning_store:
        raise HTTPException(status_code=503, detail="Lightning Store not available")
    
    if agent_id not in registered_agents:
        raise HTTPException(status_code=404, detail=f"Agent {agent_id} not registered")
    
    try:
        rollouts = await lightning_store.query_rollouts(
            status_in=[RolloutStatus.QUEUING, RolloutStatus.REQUEUING],
            sort_by="start_time",
            sort_order="asc"
        )
        
        pending_tasks = []
        for rollout in rollouts:
            if rollout.metadata and rollout.metadata.get("agentId") == agent_id:
                pending_tasks.append({
                    "id": rollout.rollout_id,
                    "name": rollout.input.get("name", ""),
                    "description": rollout.input.get("description", ""),
                    "input": rollout.input.get("input", {}),
                    "status": "Pending",
                    "priority": rollout.metadata.get("priority", 0),
                    "submittedAt": rollout.start_time.isoformat(),
                    "verificationRequired": rollout.mode == "train"
                })
        
        return pending_tasks
    
    except Exception as e:
        logger.error(f"Failed to get pending tasks: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.put("/api/tasks/{task_id}/status")
async def update_task_status(task_id: str, payload: Dict[str, Any]):
    """Update task status"""
    if not lightning_store:
        raise HTTPException(status_code=503, detail="Lightning Store not available")
    
    try:
        new_status_str = payload.get("status")
        result_data = payload.get("result")
        
        status_map = {
            "Submitted": RolloutStatus.QUEUING,
            "Pending": RolloutStatus.QUEUING,
            "InProgress": RolloutStatus.RUNNING,
            "Completed": RolloutStatus.SUCCEEDED,
            "Failed": RolloutStatus.FAILED,
            "VerificationRequired": RolloutStatus.RUNNING,
            "VerificationPassed": RolloutStatus.SUCCEEDED,
            "VerificationFailed": RolloutStatus.FAILED,
        }
        
        rollout_status = status_map.get(new_status_str, RolloutStatus.RUNNING)
        
        await lightning_store.update_rollout(
            rollout_id=task_id,
            status=rollout_status,
            metadata={"result": result_data} if result_data else None
        )
        
        logger.info(f"Task {task_id} status updated to {new_status_str}")
        return {"success": True, "taskId": task_id, "status": new_status_str}
    
    except Exception as e:
        logger.error(f"Failed to update task status: {e}")
        raise HTTPException(status_code=500, detail=str(e))

# ========== RLCS Endpoints =========
@app.post("/api/rlcs/verify", response_model=VerificationResult)
async def verify_result(payload: Dict[str, Any]):
    """Verify task result using RLCS"""
    if not RLCS_ENABLED:
        raise HTTPException(status_code=503, detail="RLCS not enabled")

    task_id = payload.get("taskId")
    result = payload.get("result")

    if not task_id or result is None:
        raise HTTPException(status_code=400, detail="Missing taskId or result")

    verification = await rlcs_verify_result(task_id, result)
    
    if lightning_store:
        try:
            status = RolloutStatus.SUCCEEDED if verification.isValid else RolloutStatus.FAILED
            await lightning_store.update_rollout(
                rollout_id=task_id,
                status=status,
                metadata={"verification": verification.model_dump()}
            )
        except Exception as e:
            logger.error(f"Failed to update rollout after verification: {e}")
    
    return verification

@app.post("/api/rlcs/validate-reasoning", response_model=ReasoningChainValidation)
async def validate_reasoning(payload: Dict[str, Any]):
    """Validate reasoning chain"""
    if not RLCS_ENABLED:
        raise HTTPException(status_code=503, detail="RLCS not enabled")

    steps_data = payload.get("steps", [])
    steps = [ReasoningStep(**step) for step in steps_data]

    return await rlcs_validate_reasoning_chain(steps)

@app.post("/api/rlcs/evaluate-confidence", response_model=ConfidenceScore)
async def evaluate_confidence(payload: Dict[str, Any]):
    """Evaluate confidence"""
    if not RLCS_ENABLED:
        raise HTTPException(status_code=503, detail="RLCS not enabled")

    content = payload.get("content", "")
    context = payload.get("context", "")

    return await rlcs_evaluate_confidence(content, context)

@app.post("/api/rlcs/verify-facts", response_model=FactCheckResult)
async def verify_facts(payload: Dict[str, Any]):
    """Verify facts"""
    if not RLCS_ENABLED:
        raise HTTPException(status_code=503, detail="RLCS not enabled")

    facts = payload.get("facts", [])
    source = payload.get("source", "")

    return await rlcs_verify_facts(facts, source)

@app.post("/api/rlcs/check-consistency", response_model=ConsistencyCheckResult)
async def check_consistency(payload: Dict[str, Any]):
    """Check consistency"""
    if not RLCS_ENABLED:
        raise HTTPException(status_code=503, detail="RLCS not enabled")

    statements = payload.get("statements", [])

    return await rlcs_check_consistency(statements)

# ========== RLCS Implementation Functions =========
async def rlcs_verify_result(task_id: str, result: str) -> VerificationResult:
    """Verify task result"""
    issues = []
    confidence = 0.8

    if len(result) < 10:
        issues.append("Result too short")
        confidence -= 0.2

    if "error" in result.lower():
        issues.append("Result contains errors")
        confidence -= 0.3

    is_valid = confidence >= RLCS_CONFIDENCE_THRESHOLD

    return VerificationResult(
        taskId=task_id,
        isValid=is_valid,
        confidence=max(0.0, confidence),
        issues=issues,
        verifiedAt=datetime.utcnow()
    )

async def rlcs_validate_reasoning_chain(steps: List[ReasoningStep]) -> ReasoningChainValidation:
    """Validate reasoning chain"""
    errors = []
    warnings = []
    total_confidence = sum(s.confidence for s in steps)
    avg_confidence = total_confidence / len(steps) if steps else 0.0

    for i, step in enumerate(steps):
        if step.stepNumber != i + 1:
            warnings.append(f"Step {i+1} numbering mismatch")
        if step.confidence < 0.5:
            warnings.append(f"Step {step.stepNumber} low confidence")

    is_valid = len(errors) == 0 and avg_confidence >= RLCS_CONFIDENCE_THRESHOLD

    return ReasoningChainValidation(
        isValid=is_valid,
        score=avg_confidence,
        errors=errors,
        warnings=warnings
    )

async def rlcs_evaluate_confidence(content: str, context: str) -> ConfidenceScore:
    """Evaluate confidence"""
    factors = {
        "content_length": min(1.0, len(content) / 500),
        "has_context": 0.8 if context else 0.3,
        "structure": 0.7 if any(m in content for m in ['\n', '.']) else 0.3
    }

    score = sum(factors.values()) / len(factors)

    return ConfidenceScore(
        score=score,
        factors=factors,
        reasoning=f"Evaluated {len(factors)} factors"
    )

async def rlcs_verify_facts(facts: List[str], source: str) -> FactCheckResult:
    """Verify facts"""
    verified = sum(1 for f in facts if any(p in f.lower() for p in ['http', 'according to']))
    unreliable = [f for f in facts if len(f) < 20]

    return FactCheckResult(
        verifiedCount=verified,
        totalCount=len(facts),
        unreliableFacts=unreliable,
        verificationScore=verified / len(facts) if facts else 0.0
    )

async def rlcs_check_consistency(statements: List[str]) -> ConsistencyCheckResult:
    """Check consistency"""
    contradictions = []

    for i, stmt1 in enumerate(statements):
        for stmt2 in statements[i+1:]:
            if "not" in stmt1 and stmt1.replace("not", "").strip() in stmt2:
                contradictions.append(f"Contradiction: '{stmt1}' vs '{stmt2}'")

    return ConsistencyCheckResult(
        isConsistent=len(contradictions) == 0,
        score=1.0 - (len(contradictions) / max(1, len(statements))),
        contradictions=contradictions
    )

# ========== VERL Training Endpoints =========
@app.post("/api/verl/initialize")
async def initialize_verl_trainer(payload: Dict[str, Any]):
    """Initialize VERL PPO trainer"""
    if not config.verl.enabled:
        raise HTTPException(status_code=503, detail="VERL not enabled")

    if not VERL_INITIALIZED or not verl_manager:
        raise HTTPException(status_code=503, detail="VERL manager not available")

    actor_model = payload.get("actorModel", config.verl.model_path)
    reward_model = payload.get("rewardModel")
    output_dir = payload.get("outputDir", "./verl_checkpoints")

    if not reward_model:
        raise HTTPException(status_code=400, detail="rewardModel is required")

    try:
        success = await verl_manager.initialize_trainer(
            actor_model_path=actor_model,
            reward_model_path=reward_model,
            output_dir=output_dir
        )

        if success:
            return {
                "success": True,
                "actorModel": actor_model,
                "rewardModel": reward_model,
                "outputDir": output_dir,
                "message": "VERL trainer initialized successfully"
            }
        else:
            raise HTTPException(status_code=500, detail="Failed to initialize VERL trainer")

    except Exception as e:
        logger.error(f"Failed to initialize VERL trainer: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/api/verl/train/start")
async def start_verl_training(payload: Dict[str, Any]):
    """Start VERL PPO training"""
    if not config.verl.enabled:
        raise HTTPException(status_code=503, detail="VERL not enabled")

    if not VERL_INITIALIZED or not verl_manager:
        raise HTTPException(status_code=503, detail="VERL manager not available")

    dataset_path = payload.get("datasetPath")
    num_steps = payload.get("numSteps", 1000)
    resume_from = payload.get("resumeFromCheckpoint")

    if not dataset_path:
        raise HTTPException(status_code=400, detail="datasetPath is required")

    try:
        result = await verl_manager.start_training(
            dataset_path=dataset_path,
            num_steps=num_steps,
            resume_from_checkpoint=resume_from
        )

        if result.get("success"):
            return result
        else:
            raise HTTPException(status_code=500, detail=result.get("error", "Unknown error"))

    except Exception as e:
        logger.error(f"Failed to start VERL training: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/api/verl/train/stop")
async def stop_verl_training():
    """Stop active VERL training"""
    if not config.verl.enabled:
        raise HTTPException(status_code=503, detail="VERL not enabled")

    if not VERL_INITIALIZED or not verl_manager:
        raise HTTPException(status_code=503, detail="VERL manager not available")

    try:
        result = await verl_manager.stop_training()

        if result.get("success"):
            return result
        else:
            raise HTTPException(status_code=500, detail=result.get("error", "Unknown error"))

    except Exception as e:
        logger.error(f"Failed to stop VERL training: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/verl/train/status")
async def get_verl_training_status():
    """Get VERL training status and metrics"""
    if not config.verl.enabled:
        raise HTTPException(status_code=503, detail="VERL not enabled")

    if not VERL_INITIALIZED or not verl_manager:
        raise HTTPException(status_code=503, detail="VERL manager not available")

    try:
        status = await verl_manager.get_training_status()
        return status

    except Exception as e:
        logger.error(f"Failed to get VERL training status: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# ========== APO Optimization Endpoints =========
@app.post("/api/apo/initialize")
async def initialize_apo_optimizer(payload: Dict[str, Any]):
    """Initialize APO prompt optimizer"""
    if not config.apo.enabled:
        raise HTTPException(status_code=503, detail="APO not enabled")

    if not APO_INITIALIZED or not apo_manager:
        raise HTTPException(status_code=503, detail="APO manager not available")

    initial_prompt = payload.get("initialPrompt")
    task_description = payload.get("taskDescription")
    validation_examples = payload.get("validationExamples", [])

    if not initial_prompt:
        raise HTTPException(status_code=400, detail="initialPrompt is required")

    if not task_description:
        raise HTTPException(status_code=400, detail="taskDescription is required")

    try:
        success = await apo_manager.initialize_optimizer(
            initial_prompt=initial_prompt,
            task_description=task_description,
            validation_examples=validation_examples
        )

        if success:
            return {
                "success": True,
                "initialPrompt": initial_prompt[:100] + "...",
                "taskDescription": task_description,
                "validationExamples": len(validation_examples),
                "message": "APO optimizer initialized successfully"
            }
        else:
            raise HTTPException(status_code=500, detail="Failed to initialize APO optimizer")

    except Exception as e:
        logger.error(f"Failed to initialize APO optimizer: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/api/apo/optimize")
async def start_apo_optimization(payload: Dict[str, Any]):
    """Start APO prompt optimization"""
    if not config.apo.enabled:
        raise HTTPException(status_code=503, detail="APO not enabled")

    if not APO_INITIALIZED or not apo_manager:
        raise HTTPException(status_code=503, detail="APO manager not available")

    num_rounds = payload.get("numRounds")
    early_stopping_threshold = payload.get("earlyStoppingThreshold", 0.01)

    try:
        result = await apo_manager.start_optimization(
            num_rounds=num_rounds,
            early_stopping_threshold=early_stopping_threshold
        )

        if result.get("success"):
            return result
        else:
            raise HTTPException(status_code=500, detail=result.get("error", "Unknown error"))

    except Exception as e:
        logger.error(f"Failed to start APO optimization: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/apo/status")
async def get_apo_optimization_status():
    """Get APO optimization status and history"""
    if not config.apo.enabled:
        raise HTTPException(status_code=503, detail="APO not enabled")

    if not APO_INITIALIZED or not apo_manager:
        raise HTTPException(status_code=503, detail="APO manager not available")

    try:
        status = await apo_manager.get_optimization_status()
        return status

    except Exception as e:
        logger.error(f"Failed to get APO optimization status: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/apo/best-prompt")
async def get_apo_best_prompt():
    """Get the current best optimized prompt"""
    if not config.apo.enabled:
        raise HTTPException(status_code=503, detail="APO not enabled")

    if not APO_INITIALIZED or not apo_manager:
        raise HTTPException(status_code=503, detail="APO manager not available")

    try:
        result = await apo_manager.get_best_prompt()

        if result.get("success"):
            return result
        else:
            raise HTTPException(status_code=404, detail=result.get("error", "No optimized prompt available"))

    except Exception as e:
        logger.error(f"Failed to get best prompt: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# ========== Main ==========
if __name__ == "__main__":
    logger.info("=" * 60)
    logger.info("🚀 Starting Lightning Server")
    logger.info("=" * 60)
    logger.info(f"  Port: {LIGHTNING_PORT}")
    logger.info(f"  Agent Lightning Available: {AGENT_LIGHTNING_AVAILABLE}")
    logger.info(f"  Lightning Store Initialized: {LIGHTNING_STORE_INITIALIZED}")
    logger.info(f"  Lightning Store Status: {'✅ READY' if lightning_store else '❌ NOT AVAILABLE'}")
    logger.info(f"  RMPT Enabled: {RMPT_ENABLED}")
    logger.info(f"  RLCS Enabled: {RLCS_ENABLED}")
    logger.info(f"  Dashboard Available: {dashboard_path.exists()}")

    if dashboard_path.exists():
        logger.info(f"  🎨 Dashboard: http://localhost:{LIGHTNING_PORT}/dashboard")

    logger.info(f"  API Docs: http://localhost:{LIGHTNING_PORT}/docs")
    logger.info("=" * 60)

    if not lightning_store:
        logger.warning("⚠️  WARNING: Lightning Store is NOT initialized!")
        logger.warning("    /api/tasks/submit will return 503 Service Unavailable")
        logger.warning("    Make sure agentlightning package is installed:")
        logger.warning("    pip install git+https://github.com/microsoft/agent-lightning.git")

    uvicorn.run(
        app,
        host="0.0.0.0",
        port=LIGHTNING_PORT,
        log_level=os.getenv("LOG_LEVEL", "info").lower()
    )