#!/bin/bash
# Start Ollama in the background
ollama serve &

echo "Waiting for Ollama to initialize on port 11434..."

# Wait until the port is open (Bash internal check)
while ! timeout 1 bash -c "echo > /dev/tcp/localhost/11434" 2>/dev/null; do
  sleep 2
done

echo "Ollama is up! Pulling Llama 3.2..."
ollama pull llama3.2

# Keep the process alive
wait