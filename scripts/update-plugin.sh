#!/bin/bash
# Update Jellyfin Invoice Plugin - customize paths below

PLUGIN_DIR="/path/to/jellyfin/plugins/JellyfinInvoice"
COMPOSE_DIR="/path/to/docker-compose"
RELEASE_URL="https://github.com/jyoung02/jellyfin-invoice-plugin/releases/latest/download/JellyfinInvoice.dll"

# Or for local development, use local build:
LOCAL_DLL="../bin/Release/net8.0/JellyfinInvoice.dll"
USE_LOCAL=false

set -e

echo "Stopping Jellyfin..."
cd "$COMPOSE_DIR"
docker compose down

echo "Updating plugin..."
rm -f "$PLUGIN_DIR/JellyfinInvoice.dll"

if [ "$USE_LOCAL" = true ]; then
    cp "$LOCAL_DLL" "$PLUGIN_DIR/"
else
    wget -q -O "$PLUGIN_DIR/JellyfinInvoice.dll" "$RELEASE_URL"
fi

echo "Starting Jellyfin..."
docker compose up -d

echo "Done! Jellyfin restarting with updated plugin."
