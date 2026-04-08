import type { FastifyReply, FastifyRequest } from "fastify";
import jwt from "jsonwebtoken";
import jwksClient from "jwks-rsa";
import type { ApiConfig } from "./config";

export function createAuthGuard(config: ApiConfig) {
  if (!config.AUTH_ENABLED) {
    return async () => {};
  }

  if (!config.ENTRA_TENANT_ID || !config.ENTRA_CLIENT_ID) {
    throw new Error(
      "AUTH_ENABLED requires ENTRA_TENANT_ID and ENTRA_CLIENT_ID to be configured.",
    );
  }

  const issuer = `https://login.microsoftonline.com/${config.ENTRA_TENANT_ID}/v2.0`;
  const audience = [
    config.ENTRA_CLIENT_ID,
    `api://${config.ENTRA_CLIENT_ID}`,
  ] as [string, string];
  const client = jwksClient({
    cache: true,
    cacheMaxEntries: 10,
    cacheMaxAge: 10 * 60 * 1000,
    jwksUri: `https://login.microsoftonline.com/${config.ENTRA_TENANT_ID}/discovery/v2.0/keys`,
    rateLimit: true,
  });

  return async (request: FastifyRequest, reply: FastifyReply) => {
    if (!request.url.startsWith("/api/")) {
      return;
    }

    const authorization = request.headers.authorization;
    if (!authorization?.startsWith("Bearer ")) {
      return reply.code(401).send({
        error: "AuthenticationRequired",
        message: "A valid Microsoft Entra access token is required.",
      });
    }

    const token = authorization.slice("Bearer ".length);
    const decoded = jwt.decode(token, { complete: true });

    if (!decoded || typeof decoded === "string" || !decoded.header.kid) {
      return reply.code(401).send({
        error: "InvalidToken",
        message: "The bearer token could not be decoded.",
      });
    }

    const signingKey = await client.getSigningKey(decoded.header.kid);

    try {
      jwt.verify(token, signingKey.getPublicKey(), {
        algorithms: ["RS256"],
        audience,
        issuer,
      });
    } catch (error) {
      request.log.warn(
        { error },
        "Rejected request with invalid Microsoft Entra token.",
      );

      return reply.code(401).send({
        error: "InvalidToken",
        message: "The supplied Microsoft Entra access token is invalid.",
      });
    }
  };
}
