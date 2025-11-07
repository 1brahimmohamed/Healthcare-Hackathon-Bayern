import express from "express";
import dotenv from "dotenv";
import crypto from "crypto";
import { createToken } from "./jwt";

dotenv.config();

const app = express();
app.use(express.json());

const PORT = process.env.PORT ? Number(process.env.PORT) : 3000;
// Keep existing ENCRYPTION_KEY support but prefer JWT_SECRET via jwt module

let SERVICE_KEYS: string[] = [];

const loadKeys = () => {
  if (SERVICE_KEYS.length) return SERVICE_KEYS;
  if (process.env.SERVICE_KEYS) {
    SERVICE_KEYS = process.env.SERVICE_KEYS.split(",")
      .map((k) => k.trim())
      .filter(Boolean);
  }
  return SERVICE_KEYS;
}

const pickRandomKey = () => {
  const keys = loadKeys();
  if (!keys.length) return null;
  return keys[Math.floor(Math.random() * keys.length)];
};

app.post("/generate", (req, res) => {
  try {
    const { email, deviceSerial, level, userId } = req.body || {};
    if (!email || typeof email !== "string") {
      return res.status(400).json({ error: "email is required" });
    }

    const timestamp = Date.now();

    const resolvedUserId =
      userId ||
      (typeof (crypto as any).randomUUID === "function"
        ? (crypto as any).randomUUID()
        : `${Date.now()}-${Math.floor(Math.random() * 1e6)}`);

    const payload = {
      email,
      deviceSerial: deviceSerial || "UNKNOWN",
      level: typeof level === "number" ? level : 1,
      // userId: resolvedUserId,
      // key: pickRandomKey(),
      timestamp,
    } as any;

    const token = createToken(payload);

    return res.json({ token });
  } catch (err: any) {
    return res.status(500).json({ error: err.message || String(err) });
  }
});

app.get("/", (_, res) => res.json({ status: "ok" }));

app.listen(PORT, () => {
  console.log(`Server listening on http://localhost:${PORT}`);
});
