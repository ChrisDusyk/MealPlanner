# flagd — the OpenFeature flag evaluation engine.
#
# Deployed to Railway as a private service (no public domain — all evaluation is
# server-side). It HTTP-syncs its flag definitions from the API's internal
# endpoint (GET /internal/feature-flags) and serves evaluations to the API and
# frontend over gRPC on port 8013.
#
# Keep this image version aligned with the Aspire flagd hosting integration
# (CommunityToolkit.Aspire.Hosting.Flagd) used in development.
FROM ghcr.io/open-feature/flagd:v0.14.2

# gRPC evaluation endpoint. Exposed on Railway's private network only.
EXPOSE 8013

# The base image's entrypoint is the flagd binary; "start" launches the daemon.
# Point flagd at the API's private-network sync endpoint via the FLAGD_SOURCES
# service variable, for example:
#   FLAGD_SOURCES=[{"uri":"http://<api-private-host>:<port>/internal/feature-flags","provider":"http"}]
CMD ["start"]
