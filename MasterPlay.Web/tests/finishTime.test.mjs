import assert from "node:assert/strict";
import test from "node:test";
import {
  finishTimeDraftFromEntry,
  finishYearMax,
  formatFinishTimeDraft,
  isFinishYearDraft,
  isValidFinishTimeDraft,
  toFinishTimeRequest,
} from "../src/lib/finishTime.ts";
import { formatFinishTime } from "../src/lib/format.ts";

const NOW = new Date("2026-09-17T00:00:00Z");

test("a year-only finish never shows a January that did not happen", () => {
  assert.equal(formatFinishTime("2019-01-01T00:00:00", false), "2019");
  assert.equal(formatFinishTime("2019-01-01T00:00:00", true), "01/2019");
});

test("reads a draft back from an entry, dropping the padding month", () => {
  assert.deepEqual(
    finishTimeDraftFromEntry({ id: 1, finishedAt: "2019-01-01T00:00:00", hasMonth: false }),
    { year: "2019", month: "" },
  );
  assert.deepEqual(
    finishTimeDraftFromEntry({ id: 2, finishedAt: "2024-03-01T00:00:00", hasMonth: true }),
    { year: "2024", month: "03" },
  );
});

test("an empty month is valid data, not an unfinished field", () => {
  assert.equal(isValidFinishTimeDraft({ year: "2019", month: "" }, NOW), true);
  assert.deepEqual(toFinishTimeRequest({ year: "2019", month: "" }), {
    finishedYear: 2019,
    finishedMonth: null,
  });
});

test("rejects a year outside the supported range", () => {
  assert.equal(isValidFinishTimeDraft({ year: "1969", month: "" }, NOW), false);
  assert.equal(isValidFinishTimeDraft({ year: "2027", month: "" }, NOW), true);
  assert.equal(isValidFinishTimeDraft({ year: "2028", month: "" }, NOW), false);
  assert.equal(finishYearMax(NOW), 2027);
});

test("rejects a half-typed or malformed year", () => {
  assert.equal(isValidFinishTimeDraft({ year: "", month: "" }, NOW), false);
  assert.equal(isValidFinishTimeDraft({ year: "20", month: "" }, NOW), false);
});

test("accepts a half-typed year while the field is being filled in", () => {
  // `isFinishYearDraft` gac phim go, `isValidFinishTimeDraft` gac nut Luu — go
  // do "20" phai qua duoc cai thu nhat va truot cai thu hai.
  assert.equal(isFinishYearDraft("20"), true);
  assert.equal(isFinishYearDraft(""), true);
  assert.equal(isFinishYearDraft("20x"), false);
  assert.equal(isFinishYearDraft("20255"), false);
});

test("rejects a month outside 1-12", () => {
  assert.equal(isValidFinishTimeDraft({ year: "2024", month: "13" }, NOW), false);
  assert.equal(isValidFinishTimeDraft({ year: "2024", month: "00" }, NOW), false);
  assert.equal(isValidFinishTimeDraft({ year: "2024", month: "12" }, NOW), true);
});

test("previews exactly what the pill will read", () => {
  assert.equal(formatFinishTimeDraft({ year: "2024", month: "03" }), "03/2024");
  assert.equal(formatFinishTimeDraft({ year: "2024", month: "" }), "2024");
  assert.equal(formatFinishTimeDraft({ year: "20", month: "" }), null);
});
