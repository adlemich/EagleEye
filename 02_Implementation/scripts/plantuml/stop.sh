#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="plantuml-server"

if podman container exists "$CONTAINER_NAME" 2>/dev/null; then
    echo "Stopping PlantUML server..."
    podman stop "$CONTAINER_NAME"
    echo "Stopped."
else
    echo "No container named '$CONTAINER_NAME' found."
fi
