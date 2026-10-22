export type UserProfile = {
  id: string;
  name: string;
  email: string;
  emailVerified: boolean;
};

export type AuthResponse = {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  refreshExpiresAtUtc: string;
  user: UserProfile;
};

import type { AnalysisSummary } from "./analyses";

export type DashboardData = {
  securityScore: number | null;
  totalAnalyses: number;
  recentAnalyses: AnalysisSummary[];
  lastAnalysis: AnalysisSummary | null;
};

export type AuthFormState = {
  success?: boolean;
  message?: string;
  fieldErrors?: Partial<Record<"name" | "email" | "password" | "confirmPassword", string>>;
  values?: { name?: string; email?: string };
};
