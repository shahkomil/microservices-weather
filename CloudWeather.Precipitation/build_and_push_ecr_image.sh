#!/bin/badh
set -e

aws ecr get-login-password --region us-east01 --profile weather-ecr-agent | docker login --username AWS --password-stdin 825772729458.dkr.ecr.us-east-1.amazonaws.com
docker build -f ./Dockerfile -t cloud-weather-precipitation:latest .
docker tag cloud-weather-precipitation:latest 825772729458.dkr.ecr.us-east-1.amazonaws.com/cloud-weather-precipitation:latest
docker push 825772729458.dkr.ecr.us-east-1.amazonaws.com/cloud-weather-precipitation:latest