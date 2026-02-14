#!/bin/bash

echo "🧪 Testing Lightning Server Integration"
echo "========================================"

# Start Lightning Server
echo "1️⃣ Starting Lightning Server..."
docker-compose up -d lightning-server

# Wait for server to be ready
echo "2️⃣ Waiting for Lightning Server to be ready..."
for i in {1..30}; do
    if curl -s http://localhost:8090/health > /dev/null; then
        echo "✅ Lightning Server is ready!"
        break
    fi
    echo "   Waiting... ($i/30)"
    sleep 2
done

# Test health endpoint
echo ""
echo "3️⃣ Testing health endpoint..."
curl -s http://localhost:8090/health | jq

# Test server info
echo ""
echo "4️⃣ Testing server info..."
curl -s http://localhost:8090/api/server/info | jq

# Register an agent
echo ""
echo "5️⃣ Registering test agent..."
curl -s -X POST http://localhost:8090/api/agents/register \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "test-agent-1",
    "agentType": "research",
    "clientId": "manual-test",
    "capabilities": {"search": true, "analyze": true}
  }' | jq

# Submit a task
echo ""
echo "6️⃣ Submitting test task..."
TASK_RESPONSE=$(curl -s -X POST http://localhost:8090/api/tasks/submit \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "test-agent-1",
    "task": {
      "id": "manual-test-task",
      "name": "Manual Test Task",
      "description": "Testing from bash script",
      "input": {"query": "test"},
      "priority": 5
    }
  }')

echo $TASK_RESPONSE | jq
TASK_ID=$(echo $TASK_RESPONSE | jq -r '.taskId')

# Verify with VERL
echo ""
echo "7️⃣ Testing VERL verification..."
curl -s -X POST http://localhost:8090/api/verl/verify \
  -H "Content-Type: application/json" \
  -d "{
    \"taskId\": \"$TASK_ID\",
    \"result\": \"This is a test result with comprehensive details\"
  }" | jq

echo ""
echo "✅ All manual tests completed!"
echo "💡 Check the logs with: docker-compose logs lightning-server"