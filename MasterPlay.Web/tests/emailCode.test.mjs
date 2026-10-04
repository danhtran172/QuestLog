import assert from "node:assert/strict";
import test from "node:test";
import { EMAIL_CODE_LENGTH, normalizeEmailCode } from "../src/lib/emailCode.ts";

test("keeps only the first six digits", () => {
  assert.equal(EMAIL_CODE_LENGTH, 6);
  assert.equal(normalizeEmailCode("123456789"), "123456");
  assert.equal(normalizeEmailCode("12a3"), "123");
});

test("pasted text with spaces or a label still yields the code", () => {
  assert.equal(normalizeEmailCode(" 123 456 "), "123456");
  assert.equal(normalizeEmailCode("Mã của bạn: 654-321"), "654321");
});
