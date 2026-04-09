#!/bin/sh
set -eu

cat <<EOF >/usr/share/nginx/html/config.js
window.__APP_CONFIG__ = {
  apiBaseUrl: "${API_BASE_URL:-http://localhost:8080}",
  speechVoice: "${SPEECH_VOICE:-en-US-JennyNeural}",
  entraTenantId: "${ENTRA_TENANT_ID:-}",
  entraClientId: "${ENTRA_CLIENT_ID:-}"
};
EOF

api_base_url_set="no"
entra_tenant_id_set="no"
entra_client_id_set="no"

if [ -n "${API_BASE_URL:-}" ]; then
  api_base_url_set="yes"
fi

if [ -n "${ENTRA_TENANT_ID:-}" ]; then
  entra_tenant_id_set="yes"
fi

if [ -n "${ENTRA_CLIENT_ID:-}" ]; then
  entra_client_id_set="yes"
fi

echo "Generated runtime config.js (API_BASE_URL set: ${api_base_url_set}, ENTRA_TENANT_ID set: ${entra_tenant_id_set}, ENTRA_CLIENT_ID set: ${entra_client_id_set})"
