import "dotenv/config";
import { buildApp } from "./app";
import { createRuntime } from "./runtime";

async function start() {
  const runtime = await createRuntime();
  const app = await buildApp(runtime);

  await app.listen({
    host: "0.0.0.0",
    port: runtime.config.PORT,
  });
}

start().catch((error) => {
  console.error(error);
  process.exit(1);
});
