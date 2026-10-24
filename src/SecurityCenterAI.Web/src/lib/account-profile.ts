export type ProfileNameState = { success?: boolean; message?: string; fieldErrors?: { name?: string } };

export function validateProfileName(data: FormData):
  | { success: true; name: string }
  | { success: false; state: ProfileNameState } {
  const submitted = data.get("name");
  const name = typeof submitted === "string" ? submitted.trim() : "";
  if (typeof submitted !== "string" || submitted.length > 200 || name.length < 2 || name.length > 100 || /[\u0000-\u001f\u007f-\u009f]/.test(name))
    return { success: false, state: { message: "Revisá el nombre para continuar.",
      fieldErrors: { name: "Usá entre 2 y 100 caracteres, sin saltos de línea o caracteres de control." } } };
  return { success: true, name };
}
