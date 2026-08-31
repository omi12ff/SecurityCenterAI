import { AuthShell } from "@/components/auth/auth-shell";
import { LoginForm } from "@/components/auth/login-form";

export default function LoginPage() {
  return (
    <AuthShell
      title="Iniciar sesion"
      subtitle="Ingresa con tu cuenta para revisar el estado de seguridad y el historial reciente."
    >
      <LoginForm />
    </AuthShell>
  );
}
