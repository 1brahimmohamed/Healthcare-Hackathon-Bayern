import jwt from "jsonwebtoken";

// Use JWT_SECRET if provided, otherwise fall back to ENCRYPTION_KEY or a generated key (dev only)
const JWT_SECRET =
  "super_secret_dev_key_change_me_super_secret_dev_key_change_me";

export function createToken(
  payload: Record<string, any>,
  options?: jwt.SignOptions
) {
  // Default to HS256 and a short expiry; caller can override via options
  // Build sign options and ensure a JWT typ header exists so the result is a JWS Compact Serialization
  const defaultOptions: jwt.SignOptions = {
    algorithm: "HS256",
    expiresIn: "24h",
  };

  const signOptions: jwt.SignOptions = {
    ...(defaultOptions as any),
    ...(options || {}),
  } as jwt.SignOptions;

  // Ensure header.typ is present (jsonwebtoken will normally add it, but be explicit)
  if (!signOptions.header) {
    signOptions.header = { typ: "JWT" } as any;
  } else if (!(signOptions.header as any).typ) {
    (signOptions.header as any).typ = "JWT";
  }

  const token = jwt.sign(
    payload,
    JWT_SECRET as jwt.Secret,
    signOptions as jwt.SignOptions
  );

  // Validate produced token is in JWS Compact Serialization: three base64url segments separated by two dots
  const compactJwsRegex = /^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/;
  if (typeof token !== "string" || !compactJwsRegex.test(token)) {
    throw new Error(
      "createToken: produced token is not a JWS Compact Serialization (three base64url segments)"
    );
  }

  console.log(JWT_SECRET);

  return token;
}

export function verifyToken(token: string) {
  try {
    return jwt.verify(token, JWT_SECRET as jwt.Secret);
  } catch (err) {
    throw err;
  }
}

export default { createToken, verifyToken };
