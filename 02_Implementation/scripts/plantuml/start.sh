#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="plantuml-server"
IMAGE="docker.io/plantuml/plantuml-server:latest"
PORT="${PLANTUML_PORT:-8180}"

if podman container exists "$CONTAINER_NAME" 2>/dev/null; then
    echo "Container '$CONTAINER_NAME' already exists. Starting it..."
    podman start "$CONTAINER_NAME"
else
    echo "Creating and starting PlantUML server on port $PORT..."
    podman run -d --name "$CONTAINER_NAME" -p "$PORT:8080" "$IMAGE"
fi

echo "PlantUML server is running at http://localhost:$PORT"
