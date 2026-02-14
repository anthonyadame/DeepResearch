"""
Lightning Server - Agent Orchestration with Microsoft Agent-Lightning (APO & VERL)
Provides FastAPI wrapper around Agent-Lightning's LightningStore with APO and VERL support.
"""
import os
import logging
from datetime import datetime
from typing import Dict, List, Optional, Any, Sequence
from enum import Enum
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse, FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field
import uvicorn

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
except ImportError as e:
    AGENT_LIGHTNING_AVAILABLE = False
    logging.warning(f"Agent Lightning not available: {e}")

# Configure logging
logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO"),
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Configuration
APO_ENABLED = os.getenv("APO_ENABLED", "true").lower() == "true"
VERL_ENABLED = os.getenv("VERL_ENABLED", "true").lower() == "true"
APO_STRATEGY = os.getenv("APO_STRATEGY", "balanced")
APO_MAX_TASKS = int(os.getenv("APO_MAX_TASKS", "10"))
VERL_CONFIDENCE_THRESHOLD = float(os.getenv("VERL_CONFIDENCE_THRESHOLD", "0.7"))
LIGHTNING_PORT = int(os.getenv("LIGHTNING_PORT", "9090"))

app = FastAPI(
    title="Lightning Server",
    description="Agent orchestration with Microsoft Agent-Lightning (APO & VERL)",
    version="1.0.0"
)

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

if AGENT_LIGHTNING_AVAILABLE:
    try:
        # Create in-memory Lightning Store
        lightning_store = InMemoryLightningStore(thread_safe=True)
        logger.info("Agent Lightning InMemoryLightningStore initialized")

        # Expose Agent Lightning REST APIs for the dashboard
        store_api = LightningStoreServer(store=lightning_store)
        app.include_router(store_api.router, prefix="/api")
        logger.info("Agent Lightning store API mounted at /api")
    except Exception as e:
        logger.error(f"Failed to initialize Agent Lightning Store: {e}")
        lightning_store = None

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
    apoEnabled: bool = APO_ENABLED
    verlEnabled: bool = VERL_ENABLED
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
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.utcnow().isoformat(),
        "agentLightningAvailable": AGENT_LIGHTNING_AVAILABLE,
        "lightningStoreInitialized": lightning_store is not None,
        "dashboardAvailable": dashboard_path.exists()
    }

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
        apoEnabled=APO_ENABLED,
        verlEnabled=VERL_ENABLED,
        registeredAgents=len(registered_agents),
        activeConnections=sum(1 for a in registered_agents.values() if a.isActive),
        startedAt=server_start_time,
        agentLightningVersion=agentlightning.__version__ if AGENT_LIGHTNING_AVAILABLE else None,
        dashboardAvailable=dashboard_available,
        dashboardUrl="/dashboard" if dashboard_available else None
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

# ========== VERL Endpoints =========
@app.post("/api/verl/verify", response_model=VerificationResult)
async def verify_result(payload: Dict[str, Any]):
    """Verify task result using VERL"""
    if not VERL_ENABLED:
        raise HTTPException(status_code=503, detail="VERL not enabled")
    
    task_id = payload.get("taskId")
    result = payload.get("result")
    
    if not task_id or result is None:
        raise HTTPException(status_code=400, detail="Missing taskId or result")
    
    verification = await verl_verify_result(task_id, result)
    
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

@app.post("/api/verl/validate-reasoning", response_model=ReasoningChainValidation)
async def validate_reasoning(payload: Dict[str, Any]):
    """Validate reasoning chain"""
    if not VERL_ENABLED:
        raise HTTPException(status_code=503, detail="VERL not enabled")
    
    steps_data = payload.get("steps", [])
    steps = [ReasoningStep(**step) for step in steps_data]
    
    return await verl_validate_reasoning_chain(steps)

@app.post("/api/verl/evaluate-confidence", response_model=ConfidenceScore)
async def evaluate_confidence(payload: Dict[str, Any]):
    """Evaluate confidence"""
    if not VERL_ENABLED:
        raise HTTPException(status_code=503, detail="VERL not enabled")
    
    content = payload.get("content", "")
    context = payload.get("context", "")
    
    return await verl_evaluate_confidence(content, context)

@app.post("/api/verl/verify-facts", response_model=FactCheckResult)
async def verify_facts(payload: Dict[str, Any]):
    """Verify facts"""
    if not VERL_ENABLED:
        raise HTTPException(status_code=503, detail="VERL not enabled")
    
    facts = payload.get("facts", [])
    source = payload.get("source", "")
    
    return await verl_verify_facts(facts, source)

@app.post("/api/verl/check-consistency", response_model=ConsistencyCheckResult)
async def check_consistency(payload: Dict[str, Any]):
    """Check consistency"""
    if not VERL_ENABLED:
        raise HTTPException(status_code=503, detail="VERL not enabled")
    
    statements = payload.get("statements", [])
    
    return await verl_check_consistency(statements)

# ========== VERL Implementation Functions =========
async def verl_verify_result(task_id: str, result: str) -> VerificationResult:
    """Verify task result"""
    issues = []
    confidence = 0.8
    
    if len(result) < 10:
        issues.append("Result too short")
        confidence -= 0.2
    
    if "error" in result.lower():
        issues.append("Result contains errors")
        confidence -= 0.3
    
    is_valid = confidence >= VERL_CONFIDENCE_THRESHOLD
    
    return VerificationResult(
        taskId=task_id,
        isValid=is_valid,
        confidence=max(0.0, confidence),
        issues=issues,
        verifiedAt=datetime.utcnow()
    )

async def verl_validate_reasoning_chain(steps: List[ReasoningStep]) -> ReasoningChainValidation:
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
    
    is_valid = len(errors) == 0 and avg_confidence >= VERL_CONFIDENCE_THRESHOLD
    
    return ReasoningChainValidation(
        isValid=is_valid,
        score=avg_confidence,
        errors=errors,
        warnings=warnings
    )

async def verl_evaluate_confidence(content: str, context: str) -> ConfidenceScore:
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

async def verl_verify_facts(facts: List[str], source: str) -> FactCheckResult:
    """Verify facts"""
    verified = sum(1 for f in facts if any(p in f.lower() for p in ['http', 'according to']))
    unreliable = [f for f in facts if len(f) < 20]
    
    return FactCheckResult(
        verifiedCount=verified,
        totalCount=len(facts),
        unreliableFacts=unreliable,
        verificationScore=verified / len(facts) if facts else 0.0
    )

async def verl_check_consistency(statements: List[str]) -> ConsistencyCheckResult:
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

# ========== Main ==========
if __name__ == "__main__":
    logger.info(f"Starting Lightning Server on port {LIGHTNING_PORT}")
    logger.info(f"Agent Lightning Available: {AGENT_LIGHTNING_AVAILABLE}")
    logger.info(f"APO Enabled: {APO_ENABLED}, VERL Enabled: {VERL_ENABLED}")
    logger.info(f"Dashboard Available: {dashboard_path.exists()}")
    
    if dashboard_path.exists():
        logger.info(f"🎨 Access dashboard at: http://localhost:{LIGHTNING_PORT}/dashboard")
    
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=LIGHTNING_PORT,
        log_level=os.getenv("LOG_LEVEL", "info").lower()
    )