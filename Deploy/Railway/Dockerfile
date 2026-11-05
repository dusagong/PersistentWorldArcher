# Build context: Builds/Railway (prepared by Deploy/Railway/Prepare-Deployment.ps1).
FROM caddy:2 AS proxy
FROM ubuntu:22.04
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates gosu libatomic1 libgcc-s1 libglib2.0-0 libstdc++6 zlib1g \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 game
COPY --from=proxy /usr/bin/caddy /usr/bin/caddy
WORKDIR /app
COPY Server/ ./Server/
COPY Caddyfile entrypoint.sh ./
RUN chmod +x /app/Server/PwaServer /app/entrypoint.sh && mkdir /data && chown game:game /data
ENV PORT=8080 PWA_GAME_PORT=7777 PWA_HEALTH_PORT=8081 PWA_MAX_PLAYERS=4 PWA_DATA_DIR=/data HOME=/home/game XDG_CONFIG_HOME=/data/config
EXPOSE 8080
ENTRYPOINT ["/app/entrypoint.sh"]
