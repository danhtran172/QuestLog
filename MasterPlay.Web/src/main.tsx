import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Toaster } from "sonner";
import App from "./App";
import { I18nProvider } from "./lib/i18n";
import { ThemeProvider } from "./lib/theme";
import { JungleProvider } from "./jungle/JungleContext";
import { TooltipProvider } from "./components/ui";
import { AuthProvider } from "./lib/auth";
import { AuthGate } from "./components/AuthGate";
import { UpdateNotice } from "./components/UpdateNotice";
import "./index.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Loi API phai noi len duoc — khong retry vo han roi im lang.
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

/**
 * Mock API chay o tang Vite dev server (xem mock-api-plugin.ts), khong phai
 * Service Worker — nen o day khong can buoc khoi tao nao.
 */
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <AuthProvider>
          <AuthGate>
            <ThemeProvider>
              {/* JungleProvider nam TRONG ThemeProvider vi no can biet theme dang
                  resolve ra gi de quyet dinh co dung san khau hay khong. */}
              <JungleProvider>
            <TooltipProvider>
              <App />
            </TooltipProvider>
              </JungleProvider>
            </ThemeProvider>
          </AuthGate>
          <Toaster position="bottom-right" richColors closeButton />
          <UpdateNotice />
        </AuthProvider>
      </I18nProvider>
    </QueryClientProvider>
  </StrictMode>,
);
