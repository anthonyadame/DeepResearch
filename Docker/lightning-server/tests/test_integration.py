"""
Integration tests for Lightning Server
"""
import pytest
import httpx
import asyncio
from datetime import datetime

BASE_URL = "http://localhost:8090"

@pytest.fixture
async def http_client():
    """Create async HTTP client for testing"""
    async with httpx.AsyncClient(base_url=BASE_URL, timeout=30.0) as client:
        yield client

@pytest.mark.asyncio
async def test_health_check(http_client):
    """Test that health endpoint responds correctly"""
    response = await http_client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert "agentLightningAvailable" in data

@pytest.mark.asyncio
async def test_server_info(http_client):
    """Test server info endpoint"""
    response = await http_client.get("/api/server/info")
    assert response.status_code == 200
    data = response.json()
    assert data["version"] == "1.0.0"
    assert "apoEnabled" in data
    assert "verlEnabled" in data

@pytest.mark.asyncio
async def test_agent_registration(http_client):
    """Test agent registration workflow"""
    # Register agent
    registration_data = {
        "agentId": "test-agent-1",
        "agentType": "research",
        "clientId": "test-client",
        "capabilities": {"search": True, "analyze": True},
        "isActive": True
    }
    
    response = await http_client.post("/api/agents/register", json=registration_data)
    assert response.status_code == 200
    data = response.json()
    assert data["agentId"] == "test-agent-1"
    
    # Retrieve agent
    response = await http_client.get(f"/api/agents/{data['agentId']}")
    assert response.status_code == 200
    retrieved = response.json()
    assert retrieved["agentType"] == "research"

@pytest.mark.asyncio
async def test_task_submission_and_retrieval(http_client):
    """Test task submission and retrieval workflow"""
    # Register agent first
    await http_client.post("/api/agents/register", json={
        "agentId": "task-test-agent",
        "agentType": "worker",
        "clientId": "test-client"
    })
    
    # Submit task
    task_data = {
        "agentId": "task-test-agent",
        "task": {
            "id": "task-1",
            "name": "Test Task",
            "description": "Testing task submission",
            "input": {"query": "test"},
            "priority": 5,
            "verificationRequired": True
        }
    }
    
    response = await http_client.post("/api/tasks/submit", json=task_data)
    assert response.status_code == 200
    result = response.json()
    assert "taskId" in result
    assert result["status"] == "Submitted"
    
    task_id = result["taskId"]
    
    # Update task status
    update_data = {
        "status": "Completed",
        "result": "Task completed successfully"
    }
    
    response = await http_client.put(f"/api/tasks/{task_id}/status", json=update_data)
    assert response.status_code == 200

@pytest.mark.asyncio
async def test_verl_verify_result(http_client):
    """Test VERL verification endpoint"""
    verification_data = {
        "taskId": "test-task-123",
        "result": "This is a comprehensive result with detailed information"
    }
    
    response = await http_client.post("/api/verl/verify", json=verification_data)
    assert response.status_code == 200
    data = response.json()
    assert "isValid" in data
    assert "confidence" in data
    assert data["taskId"] == "test-task-123"

@pytest.mark.asyncio
async def test_verl_validate_reasoning(http_client):
    """Test VERL reasoning chain validation"""
    reasoning_data = {
        "steps": [
            {
                "stepNumber": 1,
                "description": "Initial analysis",
                "logic": "Examined the input data thoroughly",
                "conclusions": ["Data is valid", "Ready for processing"],
                "confidence": 0.9
            },
            {
                "stepNumber": 2,
                "description": "Process execution",
                "logic": "Applied transformation algorithms",
                "conclusions": ["Transformation successful"],
                "confidence": 0.85
            }
        ]
    }
    
    response = await http_client.post("/api/verl/validate-reasoning", json=reasoning_data)
    assert response.status_code == 200
    data = response.json()
    assert "isValid" in data
    assert "score" in data

if __name__ == "__main__":
    pytest.main([__file__, "-v"])