docker compose down --remove-orphans

docker network prune -f

docker rm -f $(docker ps -aq)