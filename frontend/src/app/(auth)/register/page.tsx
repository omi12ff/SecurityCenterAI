import { AuthShell } from "@/components/auth/auth-shell";
import { RegisterForm } from "@/components/auth/register-form";

export default function RegisterPage() {
  return (
    <AuthShell
      title="Crear cuenta"
      subtitle="Registra tu perfil para comenzar a recibir analisis y recomendaciones de seguridad."
    >
      <RegisterForm />
    </AuthShell>
  );
}
