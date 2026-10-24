import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  distDir: process.env.SECURITY_CENTER_WEB_QA === "1" ? ".next-qa" : ".next",
  poweredByHeader: false,
  devIndicators: false,
  logging: { incomingRequests: { ignore: [/^\/(reset-password|verify-email|confirm-email-change)(\?|$)/, /^\/account\/activity(\?|$)/, /^\/tools\/scam-check(\?|$)/] }, serverFunctions: false },
  experimental: { cpus: 2 },
  async headers() {
    return [{
      source: "/:path*",
      headers: [
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
        { key: "X-Frame-Options", value: "DENY" },
      ],
    }, {
      source: "/:path(reset-password|verify-email|confirm-email-change)",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "no-store" }],
    }, {
      source: "/devices/:path*",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "no-store" }],
    }, {
      source: "/account/:path*",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "no-store" }],
    }, {
      source: "/tools/:path*",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "no-store" }],
    }, {
      source: "/getting-started/:path*",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "private, no-store" }],
    }, {
      source: "/:path(privacy|terms|report-problem)",
      headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "Cache-Control", value: "no-store" }],
    }];
  },
};

export default nextConfig;
