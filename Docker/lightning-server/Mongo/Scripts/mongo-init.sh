#!/bin/bash
# MongoDB Initialization Script for Replica Set with Authentication
# This script handles the chicken-and-egg problem of auth + replica sets

set -e

KEYFILE="/data/keyfile"
DB_PATH="/data/db"
INIT_FLAG="$DB_PATH/.mongo_initialized"

echo "MongoDB Replica Set Initialization Script"

# Set keyfile permissions
if [ -f "$KEYFILE" ]; then
    chmod 400 "$KEYFILE"
    chown mongodb:mongodb "$KEYFILE"
    echo "KeyFile permissions set"
fi

# Check if this is first initialization
if [ ! -f "$INIT_FLAG" ]; then
    echo "First initialization - setting up user and replica set"

    # Start MongoDB WITHOUT auth AND without keyFile for initial setup
    echo "Starting MongoDB without auth for initialization..."
    mongod --replSet rs0 --bind_ip_all --fork --logpath /var/log/mongodb-init.log --dbpath "$DB_PATH"

    # Wait for MongoDB to start
    echo "Waiting for MongoDB to start..."
    sleep 10

    # Initialize replica set (single node for now)
    echo "Initializing replica set..."
    mongosh --quiet --eval "
        var status = rs.status();
        if (status.ok === 0 || status.codeName === 'NotYetInitialized') {
            print('Initializing single-node replica set...');
            rs.initiate({
                _id: 'rs0',
                members: [{ _id: 0, host: 'localhost:27017' }]
            });
            print('Waiting for replica set to become PRIMARY...');
            sleep(5000);
        } else {
            print('Replica set already initialized');
        }
    " || echo "Replica set initialization may have failed"

    # Wait for node to become PRIMARY
    echo "Waiting for PRIMARY status..."
    sleep 10

    # Create admin user (now that we're PRIMARY)
    echo "Creating admin user..."
    mongosh admin --quiet --eval "
        var user = db.getUser('lightning');
        if (!user) {
            db.createUser({
                user: 'lightning',
                pwd: 'lightningpass',
                roles: [{ role: 'root', db: 'admin' }]
            });
            print('Admin user created successfully');
        } else {
            print('User lightning already exists');
        }
    "

    # Force sync to ensure user is persisted
    echo "Forcing database sync..."
    mongosh admin --quiet --eval "db.adminCommand({fsync: 1})" || true
    sleep 2

    # Shutdown MongoDB
    echo "Shutting down MongoDB..."
    mongosh admin --quiet --eval "db.shutdownServer()" || mongosh admin -u lightning -p lightningpass --quiet --eval "db.shutdownServer()" || true
    sleep 5

    # Mark as initialized
    touch "$INIT_FLAG"
    echo "Initialization complete - user created, will reconfigure replica set later"
fi

# Start MongoDB with authentication and keyFile
echo "Starting MongoDB with authentication and keyFile..."
exec mongod --replSet rs0 --bind_ip_all --auth --keyFile "$KEYFILE"

