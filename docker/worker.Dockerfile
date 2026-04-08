FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json tsconfig.base.json ./
COPY apps/api/package.json apps/api/package.json
COPY apps/worker/package.json apps/worker/package.json
COPY apps/web/package.json apps/web/package.json
COPY packages/shared/package.json packages/shared/package.json
COPY packages/orchestrator-core/package.json packages/orchestrator-core/package.json

RUN npm ci

COPY . .
RUN npm run build --workspace @ai-dev-orchestrator/shared \
  && npm run build --workspace @ai-dev-orchestrator/orchestrator-core \
  && npm run build --workspace @ai-dev-orchestrator/worker

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/package.json /app/package-lock.json /app/
COPY --from=build /app/node_modules /app/node_modules
COPY --from=build /app/apps/worker/package.json /app/apps/worker/package.json
COPY --from=build /app/apps/worker/dist /app/apps/worker/dist
COPY --from=build /app/packages/shared/package.json /app/packages/shared/package.json
COPY --from=build /app/packages/shared/dist /app/packages/shared/dist
COPY --from=build /app/packages/orchestrator-core/package.json /app/packages/orchestrator-core/package.json
COPY --from=build /app/packages/orchestrator-core/dist /app/packages/orchestrator-core/dist

CMD ["node", "apps/worker/dist/index.js"]
