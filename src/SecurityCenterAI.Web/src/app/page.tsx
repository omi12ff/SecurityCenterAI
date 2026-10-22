import { redirect } from "next/navigation";
import { readSession, readRefreshToken } from "@/lib/session";

export default async function HomePage() {
  redirect((await readSession()) ? "/dashboard" : (await readRefreshToken()) ? "/session/renew" : "/login");
}
