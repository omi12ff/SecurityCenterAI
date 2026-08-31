export type AuthUser = {
  id: string;
  name: string;
  email: string;
};

export type ProfileResponse = AuthUser & {
  createdAtUtc: string;
};

export type AuthResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
};

export type RegisterPayload = {
  name: string;
  email: string;
  password: string;
};

export type LoginPayload = {
  email: string;
  password: string;
};

export type RecentAnalysis = {
  id: string;
  title: string;
  status: "safe" | "warning" | "danger" | "unknown";
  createdAt: string;
  score: number | null;
  summary: string;
};

export type DashboardSummary = {
  securityScore: number | null;
  totalAnalyses: number;
  recentAnalyses: RecentAnalysis[];
};
