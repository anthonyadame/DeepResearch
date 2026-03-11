#!/bin/bash
# Simplified MongoDB Initialization for Replica Set
# Avoids the chicken-and-egg problem with a minimal approach

set -e

KEYFILE="/data/keyfile"
DB_PATH="/data/db"

echo "[INIT] MongoDB Startup"

# Set keyfile permissions
if [ -f "$KEYFILE" ]; then
    chmod 400 "$KEYFILE"
    chown mongodb:mongodb "$KEYFILE"
fi

# Simple approach: Start MongoDB with auth disabled initially
# MongoDB will create root user via MONGO_INITDB_ROOT_USERNAME
if [ ! -f "$DB_PATH/WiredTiger" ]; then
    echo "[INIT] First run - MongoDB will initialize with MONGO_INITDB_ROOT_USERNAME"
fi

# Start MongoDB with replica set but WITHOUT keyFile on first run
# This allows the MONGO_INITDB_ROOT_USERNAME to create the user
# After that, we manually add --keyFile
if [ -f "$DB_PATH/.rs_init_done" ]; then
    echo "[INIT] Starting with auth and keyFile"
    exec docker-entrypoint.sh mongod --replSet rs0 --bind_ip_all --auth --keyFile "$KEYFILE"
else
    echo "[INIT] Starting without keyFile for first initialization"
    exec docker-entrypoint.sh mongod --replSet rs0 --bind_ip_all
fi
