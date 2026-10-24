import { readFile } from "node:fs/promises";
import { test, expect } from "./fixtures.ts";

test("los documentos son accesibles antes de registrarse y explican sus datos pendientes", async ({ page }, testInfo) => {
  await page.goto("/");
  await page.getByRole("navigation", { name: "Información y ayuda", exact: true }).getByRole("link", { name: "Privacidad", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Privacidad", exact: true })).toBeVisible();
  const draft = page.getByRole("complementary", { name: "Estado del documento", exact: true });
  await expect(draft).toContainText("Borrador para pruebas locales"); await expect(draft).toContainText("país de operación");
  await expect(page.getByRole("main")).toContainText("copias de seguridad");
  const response = await page.goto("/terms"); expect(response?.headers()["referrer-policy"]).toBe("no-referrer");
  await expect(draft).toBeVisible(); await expect(page.getByRole("main")).toContainText("No se registra una aceptación contractual");
  await page.setViewportSize({ width: 360, height: 800 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (process.env.E2E_CAPTURE_INFORMATION === "1") await page.screenshot({ path: testInfo.outputPath("terms-draft-mobile.png"), fullPage: true });
  await page.goto("/register"); await expect(page.getByRole("form")).toContainText("borradores");
  await expect(page.getByRole("navigation", { name: "Información y ayuda", exact: true })).toBeVisible();
});

test("el reporte se revisa, copia y descarga localmente sin enviar su contenido", async ({ page, context }, testInfo) => {
  await page.goto("/report-problem"); await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  const form = page.getByRole("form", { name: "Preparar reporte local", exact: true });
  const outgoing: string[] = [];
  page.on("request", request => { if ((request.url() + (request.postData() ?? "")).includes("LOCAL_REPORT_ONLY")) outgoing.push(request.method()); });
  await expect(page.getByRole("complementary", { name: "Estado del canal de soporte", exact: true })).toContainText("no crea un ticket");
  await expect(form.locator("input[name], textarea[name], select[name]")).toHaveCount(0);
  await form.getByLabel("Dónde ocurre", { exact: true }).selectOption("analysis");
  await form.getByLabel("Resumen del problema", { exact: true }).fill("LOCAL_REPORT_ONLY: no abre la guía");
  await form.getByLabel("Pasos para reproducirlo", { exact: true }).fill("Abrir un informe de prueba");
  await form.getByLabel("Qué esperabas que ocurriera", { exact: true }).fill("Ver el resultado");
  await form.getByLabel("Qué ocurrió", { exact: true }).fill('<script>window.__problemScript=true</script>');
  await form.getByRole("button", { name: "Preparar borrador", exact: true }).click();
  await expect(form.getByRole("alert")).toContainText("Revisá");
  await form.getByRole("checkbox").check(); await expect(form.getByRole("alert")).toHaveCount(0); await form.getByRole("button", { name: "Preparar borrador", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("No se envió");
  const preview = page.getByLabel("Contenido del reporte local", { exact: true }); await expect(preview).toHaveValue(/LOCAL_REPORT_ONLY/);
  await page.getByRole("button", { name: "Copiar reporte", exact: true }).click(); expect(await page.evaluate(() => navigator.clipboard.readText())).toContain("LOCAL_REPORT_ONLY");
  const downloaded = page.waitForEvent("download"); await page.getByRole("button", { name: "Descargar reporte (JSON)", exact: true }).click();
  const download = await downloaded; expect(download.suggestedFilename()).toBe("SecurityCenterAI-reporte-local.json");
  const content = JSON.parse(await readFile((await download.path())!, "utf8"));
  expect(Object.keys(content).sort()).toEqual(["actual", "expected", "preparedAtUtc", "reportVersion", "screen", "steps", "summary"]);
  expect(content.actual).toContain("<script>"); expect(await page.evaluate(() => Object.hasOwn(window, "__problemScript"))).toBe(false);
  expect(outgoing).toEqual([]); expect(await page.evaluate(() => localStorage.length)).toBe(0);
  await page.setViewportSize({ width: 360, height: 800 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (process.env.E2E_CAPTURE_INFORMATION === "1") await page.screenshot({ path: testInfo.outputPath("problem-report-mobile.png"), fullPage: true });
  await form.getByLabel("Resumen del problema", { exact: true }).fill("Otro resumen"); await expect(preview).toHaveCount(0); await expect(form.getByRole("checkbox")).not.toBeChecked();
  await form.getByRole("button", { name: "Limpiar borrador", exact: true }).click(); await expect(form.getByLabel("Resumen del problema", { exact: true })).toHaveValue("");
});

test("sin JavaScript no se habilita un envío nativo ni se serializan los campos", async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  try {
    const page = await context.newPage(); await page.goto((process.env.E2E_BASE_URL ?? "http://localhost:3000") + "/report-problem");
    await expect(page.getByRole("button", { name: "Preparar borrador", exact: true })).toBeDisabled();
    await expect(page.getByRole("form").locator("[name]")).toHaveCount(0);
    await expect(page.getByText("Para preparar un reporte local se necesita JavaScript.", { exact: false })).toBeVisible();
  } finally { await context.close(); }
});
