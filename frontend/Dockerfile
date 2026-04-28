FROM node:22-alpine AS builder
WORKDIR /app
COPY package.json ./
COPY pnpm-lock.yaml ./
RUN npm install -g pnpm
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm build
RUN pnpm prune --prod

FROM node:22-alpine
WORKDIR /app
RUN npm install -g pnpm tsx
COPY --from=builder /app/build build/
COPY --from=builder /app/node_modules node_modules/
COPY --from=builder /app/package.json .
COPY --from=builder /app/scripts scripts/
COPY --from=builder /app/src/lib/server/auth-options.ts src/lib/server/auth-options.ts
EXPOSE 3000
ENV NODE_ENV=production
CMD [ "pnpm", "run", "start" ]
