import { test, expect, loginAccount } from "./fixtures.ts";
import { registerVerifiedOwner } from "./device-api.ts";

test("un paquete o destino ausente deja una guía útil y la renovación vuelve a Primeros pasos", async ({ page, context, account }) => {
  test.skip(process.env.E2E_AGENT_PACKAGE_UNAVAILABLE !== "1", "Requires a separate QA server with a missing package and no agent API URL.");
  await registerVerifiedOwner(account); await loginAccount(page, account); await page.goto("/getting-started");
  await expect(page.getByRole("heading", { name: "Registrá tu equipo", exact: true })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("El paquete todavía no está disponible");
  await expect(page.getByRole("main")).toContainText("La dirección para vincular el agente todavía no está configurada");
  await expect(page.getByRole("link", { name: "Descargar agente Windows (ZIP)", exact: true })).toHaveCount(0);
  const unavailable = await page.request.get("/getting-started/agent/download");
  expect(unavailable.status()).toBe(503);
  expect(await unavailable.text()).not.toMatch(/AGENT_PACKAGE_PATH|artifacts|\.env|[A-Z]:\\/);
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await page.getByRole("button", { name: "Copiar comando: Comprobar requisitos", exact: true }).click();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(".\\SecurityCenterAI.Agent.exe doctor");
  await context.clearCookies({ name: "security_center_session" });
  await page.goto("/getting-started");
  await expect(page).toHaveURL(/\/getting-started$/);
  await expect(page.getByRole("heading", { name: "Primeros pasos", exact: true })).toBeVisible();
  await page.setViewportSize({ width: 360, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
