import type { ReactNode } from "react";
import { Loader2 } from "lucide-react";
import { AuthScreen } from "./AuthScreen";
import { PasswordChangeScreen } from "./PasswordChangeScreen";
import { UsernameClaimScreen } from "./UsernameClaimScreen";
import { useAuth } from "@/lib/auth";

/**
 * Man hinh cho trong luc dang kiem tra phien dang nhap. Truoc day cho nay tra
 * thang <AuthScreen /> nen no NHAP NHAY form dang nhap mot cai roi moi vao app —
 * gio thay bang splash co icon QuestLog.
 */
function LoadingSplash() {
  return (
    <main className="grid min-h-full place-items-center bg-bg">
      <div className="flex flex-col items-center gap-5">
        <img
          src="/favicon.png"
          alt=""
          className="h-20 w-20 animate-pulse rounded-2xl object-contain drop-shadow-[0_8px_24px_color-mix(in_oklab,var(--color-primary)_35%,transparent)]"
        />
        <span className="text-lg font-extrabold tracking-tight text-fg">QuestLog</span>
        <Loader2 className="animate-spin text-primary" size={22} aria-hidden="true" />
      </div>
    </main>
  );
}

export function AuthGate({ children }: { children: ReactNode }) {
  const { status, account } = useAuth();
  if (status === "loading") return <LoadingSplash />;
  if (status !== "signedIn" && status !== "guest") return <AuthScreen />;
  if (account?.onboarding === "usernameRequired") return <UsernameClaimScreen />;
  if (account?.mustChangePassword) return <PasswordChangeScreen />;
  return children;
}
