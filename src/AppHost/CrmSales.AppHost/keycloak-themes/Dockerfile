# Custom Keycloak image with the CRM Sales login theme baked in.
# Build: docker build -t crm-keycloak -f keycloak-themes/Dockerfile keycloak-themes/
# Push:  docker tag crm-keycloak <registry>/crm-keycloak:latest && docker push ...

FROM quay.io/keycloak/keycloak:latest

# Copy the CRM theme into Keycloak's themes directory
COPY crm /opt/keycloak/themes/crm

# Keep start-dev as entrypoint for local parity; override with start + config in prod
