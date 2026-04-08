#!/bin/sh
set -eu

cat <<EOF >/usr/share/nginx/html/config.js
window.__APP_CONFIG__ = {
  apiBaseUrl: "${API_BASE_URL:-http://localhost:8080}",
  speechVoice: "${SPEECH_VOICE:-en-US-JennyNeural}"
};
EOF
