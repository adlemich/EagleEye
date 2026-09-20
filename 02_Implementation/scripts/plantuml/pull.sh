#!/usr/bin/env bash
set -euo pipefail

IMAGE="docker.io/plantuml/plantuml-server:latest"

echo "Pulling PlantUML server image..."
podman pull "$IMAGE"
echo "Done."
