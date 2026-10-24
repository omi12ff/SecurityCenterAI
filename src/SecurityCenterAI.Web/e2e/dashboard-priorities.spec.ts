import { test, expect, loginAccount } from "./fixtures.ts";
import { accountApi } from "./api.ts";
import { registerVerifiedOwner, createDevice, pairDevice } from "./device-api.ts";
import { syntheticReport, submitReport, type AgentReport, type AnalysisReceipt, type SubmittedStatus } from "./analysis-api.ts";

async function submitWhenAllowed(token: string, report: AgentReport) {
  const response = await submitReport(token, report);
  if (response.status !== 429) return response;
  const retrySeconds = Number(response.headers.get("retry-after"));
  if (!Number.isInteger(retrySeconds) || retrySeconds < 1 || retrySeconds > 60) return response;
  await response.arrayBuffer();
  await new Promise(resolve => setTimeout(resolve, retrySeconds * 1000 + 150));
  return submitReport(token, report);
}

test("el dashboard separa revisión y falta de información y enlaza la comprobación correcta", async ({ page, account, newAccount }, testInfo) => {
  test.setTimeout(120_000);
  const owner = await registerVerifiedOwner(account);
  const created = await createDevice(owner.accessToken, "Mi equipo de prioridades"); const credential = await pairDevice(created.pairingCode);
  const result = await submitWhenAllowed(credential.deviceToken, syntheticReport({ "WIN-DEFENDER-ANTIVIRUS": "attention", "WIN-BITLOCKER-SYSTEM": "unknown" }));
  expect(result.status).toBe(201); const receipt = await result.json() as AnalysisReceipt;
  const other = await registerVerifiedOwner(newAccount()); await createDevice(other.accessToken, "Equipo ajeno privado");
  await loginAccount(page, account);
  const summary = page.locator("details.dashboard-analysis");
  await expect(summary).not.toHaveAttribute("open");
  await summary.locator(":scope > summary").click();
  await expect(summary.getByRole("list", { name: "Estados de las comprobaciones", exact: true })).toBeVisible();
  await summary.locator(":scope > summary").click();
  const panel = page.getByRole("region", { name: "Por dónde empezar", exact: true });
  await expect(panel).toContainText("Mi equipo de prioridades"); await expect(page.getByRole("main")).not.toContainText("Equipo ajeno privado");
  const attention = panel.getByRole("region", { name: "Para revisar 1", exact: true });
  const unknown = panel.getByRole("region", { name: "Sin determinar 1", exact: true });
  await expect(attention.getByRole("link")).toHaveCount(1); await expect(unknown.getByRole("link")).toHaveCount(1);
  await expect(attention).not.toContainText("BitLocker"); await expect(unknown).toContainText("BitLocker");
  await expect(unknown).not.toContainText("Severidad de la regla");
  expect(await page.evaluate(() => Object.hasOwn(window, "__analysisScriptExecuted"))).toBe(false);
  const target = await unknown.getByRole("link").getAttribute("href");
  expect(target).toMatch(new RegExp(`/devices/${created.device.id}/analyses/${receipt.analysisId}#check-`));
  await unknown.getByRole("link").click();
  await expect(page).toHaveURL(new RegExp(`#check-`));
  await expect(page.locator(new URL(target!, "http://localhost").hash)).toContainText("BitLocker");
  await page.setViewportSize({ width: 360, height: 800 }); await page.goto("/dashboard");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await expect(page.getByRole("table")).toContainText("90%");
  if (process.env.E2E_CAPTURE_PRIORITIES === "1") {
    await page.screenshot({ path: testInfo.outputPath("dashboard-priorities-mobile.png"), fullPage: true });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.screenshot({ path: testInfo.outputPath("dashboard-priorities-desktop.png"), fullPage: true });
  }
  expect((await accountApi(`/api/devices/${created.device.id}/revoke`, { token: owner.accessToken, body: {} })).status).toBe(204);
  await page.reload(); await expect(panel).toContainText("acceso revocado");
  await expect(page.getByRole("table")).toContainText("acceso revocado");
});

test("informes desconocidos, no aplicables y correctos conservan sus límites en el dashboard", async ({ page, account }) => {
  test.setTimeout(120_000);
  const owner = await registerVerifiedOwner(account); const device = await createDevice(owner.accessToken); const credential = await pairDevice(device.pairingCode);
  await loginAccount(page, account);
  for (const status of ["unknown", "notApplicable", "passed"] as const) {
    const report = syntheticReport(); report.checks = report.checks.map(check => ({ ...check, status: status as SubmittedStatus }));
    expect((await submitWhenAllowed(credential.deviceToken, report)).status).toBe(201); await page.reload();
    const panel = page.getByRole("region", { name: "Por dónde empezar", exact: true });
    await expect(panel.getByRole("region", { name: "Para revisar 0", exact: true })).toBeVisible();
    if (status === "unknown") {
      await expect(panel.getByRole("region", { name: "Sin determinar 10", exact: true })).toBeVisible();
      await expect(panel).toContainText("Se muestran 3 de 10"); await expect(page.getByRole("table")).toContainText("Sin puntaje");
    } else if (status === "notApplicable") await expect(panel).toContainText("no contiene criterios aplicables");
    else await expect(panel).toContainText("Las comprobaciones aplicables se informaron correctas");
  }
});

test("el historial compacto conserva el informe más reciente y abre informes anteriores desde su enlace", async ({ page, account }) => {
  test.setTimeout(120_000);
  const owner = await registerVerifiedOwner(account); const created = await createDevice(owner.accessToken, "Equipo con historial"); const credential = await pairDevice(created.pairingCode);
  const first = await submitWhenAllowed(credential.deviceToken, syntheticReport({ "WIN-TPM-READY": "unknown" })); expect(first.status).toBe(201);
  const older = await first.json() as AnalysisReceipt;
  const second = await submitWhenAllowed(credential.deviceToken, syntheticReport()); expect(second.status).toBe(201);
  const recent = await second.json() as AnalysisReceipt;
  await loginAccount(page, account); await page.goto(`/devices/${created.device.id}`);
  const olderCard = page.locator(`#analysis-${older.analysisId}`); const recentCard = page.locator(`#analysis-${recent.analysisId}`);
  await expect(recentCard).toHaveAttribute("open", ""); await expect(olderCard).not.toHaveAttribute("open");
  await olderCard.locator(":scope > summary").click(); await expect(olderCard).toHaveAttribute("open", "");
  await olderCard.locator(":scope > summary").click(); await expect(olderCard).not.toHaveAttribute("open");
  await page.goto(`/devices/${created.device.id}#analysis-${older.analysisId}`); await expect(olderCard).toHaveAttribute("open", "");
  await expect(olderCard.getByRole("link", { name: "Entender este análisis", exact: false })).toBeVisible();
  await page.setViewportSize({ width: 360, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.goto("/dashboard");
  const row = page.getByRole("row").filter({ has: page.locator(`a[href="/devices/${created.device.id}/analyses/${older.analysisId}"]`) });
  await row.getByRole("link").click(); await expect(page).toHaveURL(new RegExp(`/analyses/${older.analysisId}$`));
  await expect(page.getByRole("heading", { name: "Entender este análisis", exact: true })).toBeVisible();
});
