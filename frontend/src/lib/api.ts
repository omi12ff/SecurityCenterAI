export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5169";

function getErrorMessage(error: unknown) {
  if (!error || typeof error !== "object") {
    return "No se pudo completar la solicitud. Intentalo nuevamente.";
  }

  const details = error as {
    title?: string;
    message?: string;
    errors?: Record<string, string[]>;
  };
  const validationMessage = details.errors
    ? Object.values(details.errors).flat().filter(Boolean).join(" ")
    : "";

  return (
    validationMessage ||
    details.title ||
    details.message ||
    "No se pudo completar la solicitud. Intentalo nuevamente."
  );
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  });

  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(getErrorMessage(error));
  }

  return response.json();
}
