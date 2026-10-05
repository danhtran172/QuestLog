import assert from "node:assert/strict";
import test from "node:test";
import { routeWithoutSession, startupRoute } from "../src/lib/authStartup.ts";

test("desktop opens straight into the app when accounts are not configured", () => {
  assert.equal(startupRoute({ desktop: true, guestThisSession: false, authConfigured: false }), "guest");
});

test("desktop tries the saved account session first", () => {
  assert.equal(startupRoute({ desktop: true, guestThisSession: false, authConfigured: true }), "restoreAccount");
});

test("desktop reload keeps an active guest session", () => {
  assert.equal(startupRoute({ desktop: true, guestThisSession: true, authConfigured: true }), "guest");
});

test("web never gets guest access", () => {
  assert.equal(startupRoute({ desktop: false, guestThisSession: true, authConfigured: true }), "restoreAccount");
  assert.equal(startupRoute({ desktop: false, guestThisSession: false, authConfigured: false }), "unconfigured");
});

test("no saved session: desktop enters the app, web shows sign in", () => {
  assert.equal(routeWithoutSession(true), "guest");
  assert.equal(routeWithoutSession(false), "signedOut");
});
