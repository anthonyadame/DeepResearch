# MongoDB Setup & Deployment Guide

This guide covers setting up and deploying Lightning Server with MongoDB persistent storage.

## Quick Start

### Option 1: Docker Compose (Recommended)

```bash
# Start Lightning Server with MongoDB replica set
cd Docker/lightning-server
docker-compose up -d

# Check logs
docker-compose logs -f lightning-server

# Verify MongoDB replica set
docker exec -it lightning-mongo1 mongosh --eval "rs.status()"

# Access services
# - Lightning Server: http://localhost:9090
# - Dashboard: http://localhost:9090/dashboard
# - API Docs: http://localhost:9090/docs
# - Health: http://localhost:9090/health
```

### Option 2: With Monitoring

```bash
# Start with Prometheus + Grafana
docker-compose --profile monitoring up -d

# Access monitoring
# - Prometheus: http://localhost:9092
# - Grafana: http://localhost:3000 (admin/admin)
```

### Option 3: With MongoDB UI

```bash
# Start with Mongo Express (MongoDB web UI)
docker-compose --profile debug up -d

# Access MongoDB UI
# - Mongo Express: http://localhost:8081 (admin/admin)
```

## MongoDB Replica Set

### Why Replica Set?

Agent-Lightning requires MongoDB in replica set mode for:
- **Transactions**: Atomic multi-document operations
- **Change Streams**: Real-time data synchronization
- **High Availability**: Automatic failover

### Replica Set Architecture

```
┌─────────────┐       ┌─────────────┐       ┌─────────────┐
│   mongo1    │◄─────►│   mongo2    │◄─────►│   mongo3    │
│  (Primary)  │       │ (Secondary) │       │ (Secondary) │
│  Port 27017 │       │  Port 27018 │       │  Port 27019 │
└─────────────┘       └─────────────┘       └─────────────┘
```

### Manual Replica Set Setup

If not using docker-compose:

```bash
# Start 3 MongoDB instances
docker run -d --name mongo1 -p 27017:27017 \
  --network lightning-net \
  mongo:7.0 --replSet rs0 --bind_ip_all

docker run -d --name mongo2 -p 27018:27017 \
  --network lightning-net \
  mongo:7.0 --replSet rs0 --bind_ip_all

docker run -d --name mongo3 -p 27019:27017 \
  --network lightning-net \
  mongo:7.0 --replSet rs0 --bind_ip_all

# Initialize replica set
docker exec -it mongo1 mongosh --eval '
  rs.initiate({
    _id: "rs0",
    members: [
      {_id: 0, host: "mongo1:27017"},
      {_id: 1, host: "mongo2:27017"},
      {_id: 2, host: "mongo3:27017"}
    ]
  })
'

# Wait for replica set to stabilize
sleep 10

# Verify status
docker exec -it mongo1 mongosh --eval "rs.status()"
```

## Environment Variables

### MongoDB Configuration

```bash
# Connection URI (replica set format)
MONGO_URI=mongodb://mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0

# Database name
MONGO_DATABASE=agentlightning

# Partition ID (for multi-trainer isolation)
MONGO_PARTITION_ID=lightning-server-1

# Connection pool settings
MONGO_MAX_POOL_SIZE=100
MONGO_MIN_POOL_SIZE=10
MONGO_CONNECT_TIMEOUT=30000
MONGO_SERVER_SELECTION_TIMEOUT=30000
```

### Server Configuration

```bash
# Server ports
LIGHTNING_PORT=9090
PROMETHEUS_PORT=9091

# Logging
LOG_LEVEL=INFO

# Metrics
PROMETHEUS_METRICS_ENABLED=true
```

## Verification & Testing

### 1. Check MongoDB Health

```bash
# Via Docker
docker exec -it lightning-mongo1 mongosh --eval "rs.status()"

# Via mongo shell (if installed locally)
mongosh "mongodb://localhost:27017,localhost:27018,localhost:27019/?replicaSet=rs0"
```

Expected output:
```json
{
  "set": "rs0",
  "members": [
    { "_id": 0, "name": "mongo1:27017", "stateStr": "PRIMARY" },
    { "_id": 1, "name": "mongo2:27017", "stateStr": "SECONDARY" },
    { "_id": 2, "name": "mongo3:27017", "stateStr": "SECONDARY" }
  ]
}
```

### 2. Check Lightning Server Health

```bash
# Health endpoint
curl http://localhost:9090/health | jq

# Expected response
{
  "status": "healthy",
  "storage": {
    "type": "mongodb",
    "initialized": true,
    "mongodb": {
      "available": true,
      "healthy": true
    }
  }
}
```

### 3. Test Store Operations

```python
import asyncio
from agentlightning import LightningStoreClient

async def test_store():
    client = LightningStoreClient("http://localhost:9090")
    
    # Enqueue a rollout
    rollout = await client.enqueue_rollout(
        input={"question": "What is 2+2?"}
    )
    print(f"Created rollout: {rollout.rollout_id}")
    
    # Query rollouts
    rollouts = await client.query_rollouts(status_in=["queuing"])
    print(f"Queued rollouts: {len(rollouts)}")
    
    await client.close()

asyncio.run(test_store())
```

## MongoDB Management

### View Data

```bash
# Connect to MongoDB
docker exec -it lightning-mongo1 mongosh "mongodb://localhost:27017/?replicaSet=rs0"

# Switch to agentlightning database
use agentlightning

# View collections
show collections

# View rollouts
db.rollouts.find().limit(5).pretty()

# Count documents
db.rollouts.countDocuments()
db.attempts.countDocuments()
db.spans.countDocuments()
```

### Create Indexes (for performance)

```javascript
// Connect to MongoDB
use agentlightning

// Create indexes for rollouts
db.rollouts.createIndex({ "rollout_id": 1 }, { unique: true })
db.rollouts.createIndex({ "status": 1 })
db.rollouts.createIndex({ "start_time": -1 })
db.rollouts.createIndex({ "mode": 1 })

// Create indexes for attempts
db.attempts.createIndex({ "rollout_id": 1, "attempt_id": 1 }, { unique: true })
db.attempts.createIndex({ "rollout_id": 1, "sequence_id": 1 })
db.attempts.createIndex({ "status": 1 })

// Create indexes for spans
db.spans.createIndex({ "rollout_id": 1, "attempt_id": 1, "sequence_id": 1 })
db.spans.createIndex({ "rollout_id": 1, "attempt_id": 1, "span_id": 1 })
db.spans.createIndex({ "trace_id": 1 })

// Create indexes for resources
db.resources.createIndex({ "resources_id": 1 }, { unique: true })
db.resources.createIndex({ "update_time": -1 })

// View index stats
db.rollouts.getIndexes()
```

### Backup & Restore

```bash
# Backup
docker exec lightning-mongo1 mongodump \
  --uri="mongodb://localhost:27017/?replicaSet=rs0" \
  --db=agentlightning \
  --archive=/data/db/backup-$(date +%Y%m%d).archive \
  --gzip

# Copy backup to host
docker cp lightning-mongo1:/data/db/backup-20240115.archive ./backups/

# Restore
docker exec lightning-mongo1 mongorestore \
  --uri="mongodb://localhost:27017/?replicaSet=rs0" \
  --archive=/data/db/backup-20240115.archive \
  --gzip
```

## Troubleshooting

### Problem: Replica set not initializing

**Symptoms**:
- `rs.status()` shows "no replset config has been received"
- Lightning Server shows "Failed to initialize MongoLightningStore"

**Solution**:
```bash
# Re-initialize replica set
docker exec -it lightning-mongo1 mongosh --eval '
  rs.initiate({
    _id: "rs0",
    members: [
      {_id: 0, host: "mongo1:27017"},
      {_id: 1, host: "mongo2:27017"},
      {_id: 2, host: "mongo3:27017"}
    ]
  })
'
```

### Problem: Connection timeout

**Symptoms**:
- "ServerSelectionTimeoutError"
- "No suitable servers found"

**Solution**:
```bash
# Check MongoDB containers are running
docker ps | grep mongo

# Check network connectivity
docker exec lightning-server ping mongo1

# Increase timeout in .env
MONGO_SERVER_SELECTION_TIMEOUT=60000
```

### Problem: PRIMARY not elected

**Symptoms**:
- All members are SECONDARY
- No PRIMARY in replica set

**Solution**:
```bash
# Force reconfiguration with priority
docker exec -it lightning-mongo1 mongosh --eval '
  cfg = rs.conf();
  cfg.members[0].priority = 2;
  rs.reconfig(cfg);
'
```

### Problem: Lightning Server falls back to in-memory

**Symptoms**:
- Logs show "Falling back to InMemoryLightningStore"
- `/health` shows `"type": "in-memory"`

**Solution**:
1. Check MongoDB is accessible
2. Verify MONGO_URI is correct
3. Ensure replica set is initialized
4. Check Lightning Server logs: `docker-compose logs lightning-server`

## Performance Tuning

### MongoDB Configuration

```javascript
// Enable WiredTiger cache size (8GB example)
db.adminCommand({
  setParameter: 1,
  wiredTigerEngineRuntimeConfig: "cache_size=8GB"
})

// Enable compression
db.adminCommand({
  setParameter: 1,
  wiredTigerCollectionBlockCompressor: "snappy"
})
```

### Connection Pool Tuning

For high-throughput scenarios:
```bash
MONGO_MAX_POOL_SIZE=200
MONGO_MIN_POOL_SIZE=50
```

For low-latency scenarios:
```bash
MONGO_MAX_POOL_SIZE=50
MONGO_MIN_POOL_SIZE=20
```

## Production Deployment

### MongoDB Atlas (Managed)

```bash
# Update MONGO_URI to use Atlas connection string
MONGO_URI=mongodb+srv://username:password@cluster.mongodb.net/?retryWrites=true&w=majority
MONGO_DATABASE=agentlightning
```

### Kubernetes Deployment

See `IMPLEMENTATION_PLAN.md` section 5.2 for Kubernetes manifests.

### Security

1. **Enable Authentication**:
   ```javascript
   use admin
   db.createUser({
     user: "admin",
     pwd: "securepassword",
     roles: ["root"]
   })
   ```

2. **Update Connection String**:
   ```bash
   MONGO_URI=mongodb://admin:securepassword@mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0&authSource=admin
   ```

3. **Enable TLS/SSL**: See MongoDB documentation for certificate setup.

## Monitoring

### MongoDB Metrics

```bash
# Install MongoDB exporter (optional)
docker run -d --name mongodb-exporter \
  --network lightning-net \
  -p 9216:9216 \
  percona/mongodb_exporter:0.40 \
  --mongodb.uri=mongodb://mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0
```

### Grafana Dashboards

Import dashboard ID: 2583 (MongoDB Dashboard for Prometheus)

## Migration from In-Memory

If migrating from InMemoryLightningStore:

1. **Phase 1**: Deploy MongoDB alongside in-memory store
2. **Phase 2**: Update MONGO_URI and restart Lightning Server
3. **Phase 3**: Verify MongoDB is being used via `/health` endpoint
4. **Phase 4**: Monitor for any issues for 24-48 hours
5. **Phase 5**: Full cutover complete

## Resources

- [MongoDB Documentation](https://docs.mongodb.com/)
- [MongoDB Replica Sets](https://www.mongodb.com/docs/manual/replication/)
- [Agent-Lightning Store API](https://microsoft.github.io/agent-lightning/stable/reference/store/)
- [MongoLightningStore Reference](https://microsoft.github.io/agent-lightning/stable/reference/store/#agentlightning.store.mongo.MongoLightningStore)
