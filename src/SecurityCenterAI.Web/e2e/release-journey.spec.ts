import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { test, expect } from "./fixtures.ts";
import { waitForAccountLink, openAccountLink } from "./mailpit.ts";
import { pairDevice, heartbeat } from "./device-api.ts";
import { syntheticReport, submitReport, type AgentReport, type AnalysisReceipt } from "./analysis-api.ts";

test.use({ actionTimeout: 15_000, navigationTimeout: 20_000 });

async function submitWhenAllowed(token: string, report: AgentReport) {
  const response = await submitReport(token, report);
  if (response.status !== 429) return response;
  const seconds = Number(response.headers.get("retry-after"));
  if (!Number.isInteger(seconds) || seconds < 1 || seconds > 60) return response;
  await response.arrayBuffer(); await new Promise(resolve => setTimeout(resolve, seconds * 1000 + 150));
  return submitReport(token, report);
}

test("recorrido 1.0: cuenta, agente simulado, resultados, comparación, herramienta, exportación y borrado", async ({ page, context, account }) => {
  test.setTimeout(180_000);
  await page.goto("/"); await page.getByRole("link", { name: "Crear cuenta gratuita", exact: false }).click();
  await page.getByLabel("Nombre completo", { exact: true }).fill(account.name);
  await page.getByLabel("Correo electrónico", { exact: true }).fill(account.email);
  await page.getByLabel("Contraseña", { exact: true }).fill(account.password);
  await page.getByLabel("Confirmar contraseña", { exact: true }).fill(account.password);
  await page.getByRole("button", { name: "Crear cuenta", exact: true }).click(); await expect(page).toHaveURL(/\/dashboard$/);
  await page.getByRole("link", { name: "Empezar con mi equipo", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Verificá tu correo", exact: true })).toBeVisible();
  await openAccountLink(page, await waitForAccountLink(account.email, "verify-email"));
  await page.getByRole("button", { name: "Confirmar correo", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("correo quedó verificado");
  await page.goto("/getting-started"); await page.getByText("Comprobar la integridad del ZIP", { exact: true }).click();
  const checksum = await page.locator(".setup-hash code").innerText();
  const agentDownload = page.waitForEvent("download"); await page.getByRole("link", { name: "Descargar agente Windows (ZIP)", exact: true }).click();
  const zip = await agentDownload; expect(createHash("sha256").update(await readFile((await zip.path())!)).digest("hex")).toBe(checksum);
  await page.getByRole("link", { name: "Registrar mi equipo", exact: true }).click();
  await page.getByLabel("Nombre del equipo", { exact: true }).fill("Equipo ficticio del recorrido 1.0");
  await page.getByRole("button", { name: "Agregar equipo", exact: true }).click();
  const credential = await pairDevice(await page.getByLabel("Código de vinculación", { exact: true }).inputValue());
  await page.goto("/getting-started"); await expect(page.getByRole("heading", { name: "Enviá tu primer análisis", exact: true })).toBeVisible();

  // Synthetic protocol observations: this test never runs scan or reads the personal agent.
  const firstReport = syntheticReport({ "WIN-DEFENDER-ANTIVIRUS": "attention", "WIN-BITLOCKER-SYSTEM": "unknown" });
  const submitted = await submitWhenAllowed(credential.deviceToken, firstReport); expect(submitted.status).toBe(201);
  const first = await submitted.json() as AnalysisReceipt;
  await page.getByRole("button", { name: "Actualizar progreso", exact: true }).click();
  await page.getByRole("link", { name: "Entender mi análisis", exact: true }).click();
  await page.getByLabel("Me ayudó", { exact: true }).check();
  await page.getByLabel("Comentario opcional", { exact: false }).fill("Valoración ficticia del recorrido integrado");
  await page.getByRole("button", { name: "Guardar valoración", exact: true }).click(); await expect(page.getByRole("region", { name: "¿Te ayudó esta guía?", exact: true }).getByRole("status")).toContainText("quedó guardada");
  const individualDownload = page.waitForEvent("download"); await page.getByRole("link", { name: "Exportar análisis (JSON)", exact: true }).click();
  const exportedAnalysis = JSON.parse(await readFile((await (await individualDownload).path())!, "utf8"));
  expect(exportedAnalysis.analysis.id).toBe(first.analysisId); expect(exportedAnalysis.feedback.rating).toBe("helpful");

  const secondReport = syntheticReport(); const next = await submitWhenAllowed(credential.deviceToken, secondReport); expect(next.status).toBe(201);
  const second = await next.json() as AnalysisReceipt;
  await page.goto("/dashboard"); await expect(page.getByRole("region", { name: "Por dónde empezar", exact: true })).toContainText("se informaron correctas");
  await page.getByRole("link", { name: "Ver historial por equipo", exact: false }).click();
  await expect(page).toHaveURL(/\/devices$/);
  const historyLink = page.getByRole("link", { name: "Ver equipo e historial", exact: false });
  await expect(historyLink).toHaveAttribute("href", `/devices/${credential.deviceId}`);
  await historyLink.click(); await expect(page).toHaveURL(new RegExp(`/devices/${credential.deviceId}$`));
  await page.getByRole("link", { name: "Volver a dispositivos", exact: false }).click(); await expect(page).toHaveURL(/\/devices$/);
  await page.getByRole("heading", { name: "Equipo ficticio del recorrido 1.0", exact: true }).getByRole("link").click();
  await expect(page).toHaveURL(new RegExp(`/devices/${credential.deviceId}$`));
  await page.getByRole("button", { name: "Actualizar", exact: true }).click();
  await expect(page.getByRole("link", { name: "Comparar análisis", exact: true })).toBeVisible();
  await page.getByRole("link", { name: "Comparar análisis", exact: true }).click();
  await page.getByLabel("Análisis anterior", { exact: true }).selectOption(first.analysisId);
  await page.getByLabel("Análisis reciente", { exact: true }).selectOption(second.analysisId);
  await page.getByRole("button", { name: "Comparar", exact: true }).click(); await expect(page.getByRole("main")).toContainText("Mejoras observadas");
  await page.goto(`/devices/${credential.deviceId}/analyses/${first.analysisId}`);
  await page.getByLabel("Confirmo que quiero eliminar este análisis", { exact: true }).check();
  await page.getByRole("button", { name: "Eliminar análisis", exact: true }).click(); await expect(page.getByRole("status")).toContainText("Se eliminó el análisis");
  expect((await submitWhenAllowed(credential.deviceToken, firstReport)).status).toBe(410);

  await page.getByRole("link", { name: "Enlaces y mensajes", exact: true }).click();
  await page.getByLabel("Tipo de contenido", { exact: true }).selectOption("message");
  await page.getByLabel("Texto del mensaje", { exact: true }).fill("URGENTE: ingresá tu contraseña. RECORRIDO_LOCAL_FICTICIO");
  await page.getByRole("checkbox").check(); await page.getByRole("button", { name: "Revisar contenido", exact: true }).click();
  await expect(page.getByRole("region", { name: "Señales para revisar", exact: true })).toContainText("Referencia a claves o códigos");
  await page.goto("/account");
  const accountDownload = page.waitForEvent("download"); await page.getByRole("link", { name: "Exportar mi cuenta (JSON)", exact: true }).click();
  const exportedAccount = JSON.parse(await readFile((await (await accountDownload).path())!, "utf8"));
  expect(exportedAccount.profile.email).toBe(account.email); expect(exportedAccount.analyses).toHaveLength(1);
  expect(exportedAccount.analyses[0].analysis.id).toBe(second.analysisId); expect(exportedAccount.linkMessageUsage.checks).toBe(1);
  expect(exportedAccount.deletedReports).toHaveLength(1); expect(exportedAccount.deletedReports[0].reportId).toBe(first.reportId);
  expect(JSON.stringify(exportedAccount)).not.toContain("RECORRIDO_LOCAL_FICTICIO");
  const removal = page.getByRole("region", { name: "Eliminar mi cuenta", exact: true });
  await removal.getByLabel("Contraseña actual", { exact: true }).fill(account.password);
  await removal.getByLabel("Escribí ELIMINAR para confirmar", { exact: true }).fill("ELIMINAR");
  await removal.getByRole("button", { name: "Eliminar mi cuenta", exact: true }).click();
  await expect.poll(async () => {
    if (/\/login\?reason=account-deleted$/.test(page.url())) return "deleted";
    const errors = removal.getByRole("alert");
    return await errors.count() ? await errors.allTextContents() : "pending";
  }, { timeout: 15_000 }).toBe("deleted");
  expect((await heartbeat(credential.deviceToken)).status).toBe(401);
  expect((await context.cookies()).some(cookie => cookie.name.startsWith("security_center_"))).toBe(false);
});
