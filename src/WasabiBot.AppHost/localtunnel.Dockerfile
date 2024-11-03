FROM denoland/deno:alpine
RUN deno install -g --allow-env --allow-read --allow-net npm:localtunnel
ENTRYPOINT ["localtunnel"]
