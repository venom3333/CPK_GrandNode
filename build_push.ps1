$REGISTRY_ID = (yc container registry get --name alrosa-test-container-registry  --format json | ConvertFrom-Json).id
docker build -t cr.yandex/${REGISTRY_ID}/cpk:development .
docker push cr.yandex/${REGISTRY_ID}/cpk:development
kubectl rollout restart deployment/cpk-dev-deployment
CMD /c PAUSE