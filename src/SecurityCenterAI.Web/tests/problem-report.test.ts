import test from "node:test";
import assert from "node:assert/strict";
import { prepareProblemReport } from "../src/lib/problem-report.ts";

const input = () => ({ screen: "analysis", summary: " No pude abrir el resultado ", steps: "1. Abrir el informe\n2. Elegir la guía",
  expected: "Ver una explicación", actual: "Aparece un aviso", reviewed: true });
const now = new Date("2026-10-24T15:00:00Z");

test("a local report preserves reviewed text and only the explicitly permitted fields", () => {
  const result = prepareProblemReport({ ...input(), email: "PRIVATE_EMAIL", token: "PRIVATE_TOKEN", url: "PRIVATE_URL", userAgent: "PRIVATE_BROWSER" }, now);
  assert.ok(result.report); assert.equal(result.report.summary, "No pude abrir el resultado");
  assert.equal(result.report.steps, input().steps); assert.equal(result.report.preparedAtUtc, now.toISOString());
  assert.deepEqual(Object.keys(result.report).sort(), ["actual", "expected", "preparedAtUtc", "reportVersion", "screen", "steps", "summary"]);
  assert.ok(!JSON.stringify(result.report).includes("PRIVATE_"));
});

test("the report requires an explicit review and an allowed screen", () => {
  for (const reviewed of [false, "true", undefined]) assert.ok(prepareProblemReport({ ...input(), reviewed }, now).errors?.reviewed);
  for (const screen of ["", "../account", "https://example.invalid/?token=PRIVATE", null]) assert.ok(prepareProblemReport({ ...input(), screen }, now).errors?.screen);
});

test("all text fields are bounded and errors never echo their contents", () => {
  for (const [key, maximum] of [["summary", 160], ["steps", 2000], ["expected", 1000], ["actual", 1000]] as const) {
    assert.ok(prepareProblemReport({ ...input(), [key]: "x".repeat(maximum) }, now).report);
    for (const text of ["x".repeat(maximum + 1), "  ", "PRIVATE\u0000VALUE", { private: "PRIVATE" }]) {
      const result = prepareProblemReport({ ...input(), [key]: text }, now);
      assert.ok(result.errors?.[key]); assert.ok(!JSON.stringify(result).includes("PRIVATE"));
    }
  }
});

test("invalid shapes and clocks do not produce partial artifacts", () => {
  for (const value of [null, "text", [], new FormData()]) assert.ok(prepareProblemReport(value, now).errors);
  assert.ok(prepareProblemReport(input(), new Date("invalid")).errors);
});

test("the local JSON keeps Unicode and literal markup without transforming it into commands or HTML", () => {
  const result = prepareProblemReport({ ...input(), actual: '<script>literal</script> · á ñ 😀\n$(literal)' }, now);
  assert.ok(result.report); assert.equal(JSON.parse(JSON.stringify(result.report)).actual, '<script>literal</script> · á ñ 😀\n$(literal)');
});
