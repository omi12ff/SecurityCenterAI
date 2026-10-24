export const problemScreens = [
  { value: "account", label: "Cuenta y acceso" }, { value: "setup", label: "Primeros pasos" },
  { value: "devices", label: "Dispositivos y vinculación" }, { value: "analysis", label: "Análisis y guías" },
  { value: "dashboard", label: "Resumen e historial" }, { value: "messages", label: "Enlaces y mensajes" },
  { value: "other", label: "Otra pantalla" },
] as const;
export type ProblemScreen = typeof problemScreens[number]["value"];
export type ProblemFields = { screen: string; summary: string; steps: string; expected: string; actual: string; reviewed: boolean };
export type ProblemReport = { reportVersion: 1; preparedAtUtc: string; screen: ProblemScreen; summary: string; steps: string; expected: string; actual: string };
export type ProblemErrors = Partial<Record<keyof ProblemFields, string>>;
export const emptyProblemFields = (): ProblemFields => ({ screen: "", summary: "", steps: "", expected: "", actual: "", reviewed: false });

/** Local artifact only. Do not add browser, account, URL, logs or credentials automatically. */
export function prepareProblemReport(value: unknown, now = new Date()): { report: ProblemReport; errors?: never } | { report?: never; errors: ProblemErrors } {
  if (!value || typeof value !== "object" || Array.isArray(value)) return { errors: { summary: "Completá el resumen del problema." } };
  const input = value as Record<string, unknown>;
  const errors: ProblemErrors = {};
  const text = (key: "summary" | "steps" | "expected" | "actual", max: number) => {
    const raw = input[key]; const result = typeof raw === "string" ? raw.trim() : "";
    if (!result || result.length > max || /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/.test(result)) errors[key] = `Completá este campo con hasta ${max} caracteres de texto.`;
    return result;
  };
  const summary = text("summary", 160); const steps = text("steps", 2000); const expected = text("expected", 1000); const actual = text("actual", 1000);
  if (!problemScreens.some(screen => screen.value === input.screen)) errors.screen = "Elegí en qué parte de la aplicación ocurre.";
  if (input.reviewed !== true) errors.reviewed = "Revisá que el borrador no incluya datos sensibles y confirmalo para prepararlo.";
  if (!Number.isFinite(now.getTime())) errors.summary = "No pudimos preparar la fecha del reporte. Revisá el reloj de este equipo.";
  if (Object.keys(errors).length) return { errors };
  return { report: { reportVersion: 1, preparedAtUtc: now.toISOString(), screen: input.screen as ProblemScreen, summary, steps, expected, actual } };
}
